using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Toolkit.Uwp.Notifications;

using Serilog;
using Serilog.Events;

using WebRtcVadSharp;

using WinWhisper.Models;
using WinWhisper.Services;
using WinWhisper.Services.Abstractions;
using WinWhisper.Services.Transcription;
using WinWhisper.ViewModels;
using WinWhisper.Views.Windows;

using WinForms = System.Windows.Forms;


namespace WinWhisper;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static Microsoft.Extensions.Hosting.IHost Host { get; private set; } = default!;

    private WinForms.NotifyIcon? _notifyIcon;
    private System.Drawing.Icon? _defaultIcon;
    private SettingsWindow? _openSettingsWindow;

    // Status window active flag for HotKeyManager to check asynchronously
    public volatile bool IsStatusWindowOpen;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Dll Import Resolver for webrtcvad
        NativeLibrary.SetDllImportResolver(typeof(WebRtcVad).Assembly, (libraryName, assembly, searchPath) =>
        {
            if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var handle))
            {
                return handle;
            }
            else if (libraryName.Equals("webrtcvad", StringComparison.CurrentCultureIgnoreCase))
            {
                var exeLocation = Assembly.GetExecutingAssembly().Location;
                var exeDirectory = Path.GetDirectoryName(exeLocation) ?? string.Empty;
                var rid = RuntimeInformation.RuntimeIdentifier;
                var nativePath = Path.Combine(exeDirectory, "runtimes", rid, "native", "webrtcvad.dll");
                if (File.Exists(nativePath))
                {
                    return NativeLibrary.Load(nativePath);
                }
            }
            return IntPtr.Zero;
        });

        // Serilog logger configuration
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console();

#if !DEBUG
        // Enable file logging in Release builds
        var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinWhisper", "logs");
        Directory.CreateDirectory(logDirectory);

        loggerConfig.WriteTo.File(
            path: Path.Combine(logDirectory, "app-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
#endif

        Log.Logger = loggerConfig.CreateLogger();
        Log.Information("WinWhisper Started!");

        var configManager = new SettingsManager(Log.ForContext<SettingsManager>());
        await configManager.LoadAsync();

        Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Configuration Service
                services.AddSingleton(configManager);
                services.AddSingleton(sp => sp.GetRequiredService<SettingsManager>().Configuration);
                services.AddSingleton<IOptionsMonitor<UserSettings>>(sp => sp.GetRequiredService<SettingsManager>());

                // Model Service
                services.AddSingleton<IModelService, ModelService>();

                // Transcription Providers
                services.AddSingleton<LocalWhisperProvider>();
                services.AddHostedService(sp => sp.GetRequiredService<LocalWhisperProvider>());
                services.AddSingleton<OpenAIWhisperProvider>();

                // Transcription Provider Factory
                services.AddSingleton<TranscriptionProviderFactory>();

                // HotKey Manager
                services.AddSingleton<HotKeyManager>();
                services.AddHostedService(sp => sp.GetRequiredService<HotKeyManager>());

                // Audio Service
                services.AddSingleton<SoundEffectService>();
                services.AddHostedService(sp => sp.GetRequiredService<SoundEffectService>());

                // Last transcription cache
                services.AddSingleton<LastTranscriptionStore>();

                // Transient Services
                services.AddTransient<TranscriptionService>();
                services.AddTransient<StatusWindow>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<SettingsWindow>();
            })
            .Build();

        var hotKeyManager = Host.Services.GetRequiredService<HotKeyManager>();

        hotKeyManager.OnActivate += (sender, e) => Dispatcher.InvokeAsync(() =>
            {
                if (Current.MainWindow is StatusWindow statusWindow)
                {
                    _ = statusWindow.StopTranscriptionService(false);
                }
                else if (Current.MainWindow is null)
                {
                    Current.MainWindow = statusWindow = Host.Services.GetRequiredService<StatusWindow>();
                    statusWindow.StartTranscriptionService();
                }
            });

        hotKeyManager.OnCancel += (sender, e) =>
        {
            if (!IsStatusWindowOpen)
                return;
            e.Cancel = true;
            Dispatcher.InvokeAsync(() =>
            {
                if (Current.MainWindow is StatusWindow statusWindow)
                {
                    _ = statusWindow.StopTranscriptionService(true);
                }
            });
        };

        hotKeyManager.OnHookStatusChanged += (sender, isHealthy) => Dispatcher.InvokeAsync(() => UpdateTrayIcon(isHealthy));

        await Host.StartAsync();

        CreateNotifyIcon();

        // First run: no backend configured → open Settings so the user can pick one
        // before their first hotkey press silently no-ops.
        var initialConfig = configManager.Configuration;
        if (!initialConfig.Model.Api.Enabled && initialConfig.Model.Local.GgmlType == null)
        {
            Log.Information("First run detected (no transcription backend configured). Opening Settings window.");
            ShowSettingsDialog();
        }
    }

    private void ShowSettingsDialog()
    {
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (_openSettingsWindow != null)
            {
                _openSettingsWindow.Activate();
                return;
            }

            _openSettingsWindow = Host.Services.GetRequiredService<SettingsWindow>();
            try
            {
                _openSettingsWindow.ShowDialog();
            }
            finally
            {
                _openSettingsWindow = null;
            }
        });
    }

    public static void ShowTrayNotification(string title, string message)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to show toast notification");
        }
    }

    private void CreateNotifyIcon()
    {
        _notifyIcon = new WinForms.NotifyIcon()
        {
            Text = "WinWhisper",
            Visible = true
        };

        var streamInfo = GetResourceStream(new Uri("pack://application:,,,/Assets/Images/ww-logo.ico"));
        using (var stream = streamInfo.Stream)
        {
            _notifyIcon.Icon = new System.Drawing.Icon(stream);
        }
        _defaultIcon = _notifyIcon.Icon;

        var contextMenu = new WinForms.ContextMenuStrip();
        contextMenu.Items.Add("Copy last transcription", null, CopyLastTranscriptionMenuItem_Click);
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Options", null, OptionsMenuItem_Click);
        contextMenu.Items.Add("-");
        contextMenu.Items.Add("Exit", null, ExitMenuItem_Click);

        _notifyIcon.ContextMenuStrip = contextMenu;

        _notifyIcon.MouseClick += (s, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
            {
                ShowSettingsDialog();
            }
        };
    }

    private void UpdateTrayIcon(bool isHealthy)
    {
        if (_notifyIcon == null) return;

        if (isHealthy)
        {
            _notifyIcon.Icon = _defaultIcon;
            _notifyIcon.Text = "WinWhisper";
        }
        else
        {
            // Create warning overlay
            using var bitmap = _defaultIcon!.ToBitmap();
            using (var g = System.Drawing.Graphics.FromImage(bitmap))
            {
                var warningIcon = System.Drawing.SystemIcons.Warning;
                // Draw warning at bottom-right, scaled to 66%
                var size = bitmap.Width * 2 / 3;
                g.DrawIcon(warningIcon, new System.Drawing.Rectangle(bitmap.Width - size, bitmap.Height - size, size, size));
            }
            _notifyIcon.Icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
            _notifyIcon.Text = "WinWhisper - Reconnecting keyboard hook...";
        }
    }

    private void OptionsMenuItem_Click(object? sender, EventArgs e)
    {
        ShowSettingsDialog();
    }

    private void CopyLastTranscriptionMenuItem_Click(object? sender, EventArgs e)
    {
        var text = Host.Services.GetRequiredService<LastTranscriptionStore>().Text;
        if (string.IsNullOrEmpty(text)) return;

        try
        {
            Clipboard.SetText(text);
            ShowTrayNotification("WinWhisper", "Last transcription copied to clipboard.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to copy last transcription to clipboard");
        }
    }

    private void ExitMenuItem_Click(object? sender, EventArgs e)
    {
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _notifyIcon?.Dispose();

        if (Host != null)
        {
            if (Host.Services.GetService<HotKeyManager>() is IAsyncDisposable hotKeyManager)
            {
                hotKeyManager.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            Host.StopAsync().GetAwaiter().GetResult();
            Host.Dispose();
        }

        base.OnExit(e);
    }
}