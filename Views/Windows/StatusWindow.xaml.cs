using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

using WinWhisper.Common.Threading;
using WinWhisper.Infrastructure.Keyboard;
using WinWhisper.Models;
using WinWhisper.Services;
using WinWhisper.Services.Transcription;
using WinWhisper.Views.Animations;

using static Windows.Win32.PInvoke;

namespace WinWhisper.Views.Windows;

public enum StatusType
{
    Recording,
    Transcribing,
    Idle,
};

/// <summary>
/// Interaction logic for StatusWindow.xaml
/// </summary>
public partial class StatusWindow : Window
{
    private readonly Storyboard _showAnimation;
    private readonly Storyboard _leadingIconAnimation;
    private MouseFollower? _mouseFollower;
    private readonly TranscriptionService _transcriptionService;
    private readonly UserSettings.PostProcessingSettings _settings;

    public StatusWindow(TranscriptionService transcriptionService, UserSettings config)
    {
        InitializeComponent();

        _transcriptionService = transcriptionService;
        _transcriptionService.TranscriptionEvent += HandleTranscriptionEvent;
        _settings = config.PostProcessing;

        _showAnimation = (Storyboard)Resources["ShowAnimation"];
        _leadingIconAnimation = (Storyboard)Resources["LeadingIconAnimation"];
    }

    public void StartTranscriptionService()
    {
        _transcriptionService.Start();
    }

    public Task StopTranscriptionService(bool aborted = false)
    {
        return _transcriptionService.Stop(aborted);
    }

    private void HandleTranscriptionEvent(object? sender, TranscriptionEventArgs e)
    {
        switch (e.Type)
        {
            case TranscriptionEventType.RecordingStarted:
                Dispatcher.RunOnMainThread(() => OnRecordingStarted((string)e.Payload!));
                break;
            case TranscriptionEventType.LanguageChanged:
                Dispatcher.RunOnMainThread(() => OnLanguageChanged((string)e.Payload!));
                break;
            case TranscriptionEventType.VoiceDetected:
                Dispatcher.RunOnMainThread(OnVoiceActivityDetected);
                break;
            case TranscriptionEventType.TranscribingStarted:
                Dispatcher.RunOnMainThread(OnTranscribingStarted);
                break;
            case TranscriptionEventType.TranscribingCompleted:
                Dispatcher.RunOnMainThread(() => OnTranscribingCompleted((string)e.Payload!));
                break;
            case TranscriptionEventType.Finished:
                Dispatcher.RunOnMainThread(() => OnTranscriptionServiceFinished((string?)e.Payload));
                break;
        }
    }

    private void StatusWindow_SourceInitialized(object sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong((HWND)hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        exStyle |= (int)WINDOW_EX_STYLE.WS_EX_TRANSPARENT; // mouse click through
        exStyle |= (int)WINDOW_EX_STYLE.WS_EX_NOACTIVATE; // disable window activation
        _ = SetWindowLong((HWND)hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle);
    }

    private void StatusWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_mouseFollower == null)
        {
            var bbox = new Rect(-Width / 2, 0, Width, Height);
            _mouseFollower = new MouseFollower(this, bbox, 0.7);
            _mouseFollower.Start(true);
        }

        SoundEffectService.Instance.Play("Speech On", 0.2f);

        // Initially hide ProgressFill
        ProgressFill.Fade(false, true);

        // play show animation
        _showAnimation.Begin(this, true);

        if (Application.Current is App app)
        {
            app.IsStatusWindowOpen = true;
        }
    }

    private void StatusWindow_Closing(object? sender, CancelEventArgs e)
    {
        _mouseFollower?.Stop();
        _mouseFollower = null;

        _transcriptionService.TranscriptionEvent -= HandleTranscriptionEvent;
        _transcriptionService.Dispose();

        SoundEffectService.Instance.Play("Speech Off", 0.2f);

        if (Application.Current is App app)
        {
            app.IsStatusWindowOpen = false;
        }
    }

    public void VAIndicator_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Show/Hide LeadingIcon
        bool shouldShow = VAIndicator.Visibility != Visibility.Visible;
        var scaleXAnim = (DoubleAnimation)_leadingIconAnimation.Children[0];
        var scaleYAnim = (DoubleAnimation)_leadingIconAnimation.Children[1];
        scaleXAnim.From = scaleYAnim.From = shouldShow ? 0.0 : 1.0;
        scaleXAnim.To = scaleYAnim.To = shouldShow ? 1.0 : 0.0;
        _leadingIconAnimation.Begin(this, true);
    }

    private StatusType _status = StatusType.Idle;
    private readonly Stopwatch _statusChangeStopwatch = Stopwatch.StartNew();

    private void OnRecordingStarted(string lang)
    {
        if (_status == StatusType.Recording)
            return;

        _status = StatusType.Recording;
        _statusChangeStopwatch.Restart();

        LeadingIcon.Source = IconImages.Value["microphone"];
        VAIndicator.Visibility = Visibility.Collapsed;
        _leadingIconAnimation.SafeSkipToFill(this);
        StatusText.Text = _recordingStatusTexts.TryGetValue(lang, out string? value) ? value : "listening...";
        ProgressFill.Fade(false);

        Show();
    }

    private void OnLanguageChanged(string lang)
    {
        if (_status != StatusType.Recording)
            return;

        _statusChangeStopwatch.Restart();
        VAIndicator.Visibility = Visibility.Collapsed;
        _leadingIconAnimation.SafeSkipToFill(this);
        StatusText.Text = _recordingStatusTexts.TryGetValue(lang, out string? value) ? value : "listening...";
    }

    private void OnVoiceActivityDetected()
    {
        if (_status == StatusType.Recording)
        {
            // Wait for 1 second to prevent keyboard sounds or sound effects from being incorrectly detected as voice activity
            if (_statusChangeStopwatch.ElapsedMilliseconds > 1000)
                VAIndicator.ShowLinger();
        }
    }

    private void OnTranscribingStarted()
    {
        _status = StatusType.Transcribing;
        _statusChangeStopwatch.Restart();
        LeadingIcon.Source = IconImages.Value["pencil"];
        VAIndicator.Visibility = Visibility.Collapsed;
        _leadingIconAnimation.SafeSkipToFill(this);
        StatusText.Text = "Transcribing...";
        ProgressFill.Fade(true);
    }

    private async Task OnTranscribingCompleted(string transcription)
    {
        if (_settings.InputMethod == "keyboard")
        {
            foreach (var ch in transcription)
            {
                if (ch == '\n' || ch == '\r')
                {
                    // Simulate Enter key press for new lines
                    var enterKey = new KeyCode(VIRTUAL_KEY.VK_RETURN);
                    KeyboardSimulator.Press(enterKey);
                    KeyboardSimulator.Release(enterKey);
                }
                else
                {
                    // Simulate typing the character
                    KeyboardSimulator.TypeUnicode(ch.ToString());
                }

                // slight delay between characters
                await Task.Delay(Math.Clamp(_settings.WritingKeyPressDelayMs, 0, 100));
            }
        }
        else
        {
            // use clipboard paste (Ctrl+V)
            Clipboard.SetText(transcription);
            // send Ctrl+V key event
            var ctrlKey = new KeyCode(VIRTUAL_KEY.VK_LCONTROL);
            var vKey = new KeyCode(VIRTUAL_KEY.VK_V);
            KeyboardSimulator.Press(ctrlKey);
            KeyboardSimulator.Press(vKey);
            await Task.Delay(100);
            KeyboardSimulator.Release(vKey);
            KeyboardSimulator.Release(ctrlKey);
        }
    }

    private void OnTranscriptionServiceFinished(string? errorMessage)
    {
        _status = StatusType.Idle;
        _statusChangeStopwatch.Restart();
        Close();

        if (errorMessage != null)
        {
            App.ShowTrayNotification("Transcription failed", errorMessage);
        }
    }

    private static readonly Dictionary<string, string> _recordingStatusTexts = new() {
        { "ko", "가나다..." },
        { "en", "abc..." },
        { "ja", "聞いてる..." },
        { "zh", "听着..." },
        { "zh-TW", "聽著..." },
        { "es", "escuchando..." },
        { "fr", "écoute..." },
        { "de", "hört..." },
        { "it", "ascolta..." },
        { "pt", "ouvindo..." },
        { "ru", "слушает..." },
        { "ar", "يستمع..." },
        { "hi", "सुन रहा..." },
        { "th", "ฟังอยู่..." },
        { "vi", "đang nghe..." }
    };

    private static readonly Lazy<IReadOnlyDictionary<string, BitmapImage>> IconImages = new(() =>
    {
        var images = new Dictionary<string, BitmapImage>();
        foreach (var (key, path) in new Dictionary<string, string>
        {
            { "microphone", "/Assets/Images/microphone.png" },
            { "pencil", "/Assets/Images/pencil.png" }
        })
        {
            images[key] = new BitmapImage(new Uri(path, UriKind.Relative));
        }
        return images;
    });
}
