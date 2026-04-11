using System.IO;

using Microsoft.Extensions.Options;

using Serilog;

using WinWhisper.Models;

namespace WinWhisper.Services;

/// <summary>
/// Service responsible for loading and saving user configuration
/// </summary>
public class SettingsManager : IOptionsMonitor<UserSettings>
{
    private readonly ILogger _logger;
    private readonly string _directory;
    private readonly string _fullPath;
    private UserSettings _settings;
    private readonly List<Action<UserSettings, string?>> _listeners = [];
    private readonly List<Func<UserSettings, string?, Task>> _asyncListeners = [];

    /// <summary>
    /// Gets the current user configuration
    /// </summary>
    public UserSettings Configuration => _settings;

    /// <summary>
    /// Gets the current value (IOptionsMonitor interface implementation)
    /// </summary>
    public UserSettings CurrentValue => _settings;

    public SettingsManager(ILogger logger)
    {
        _logger = logger;

        // Use standard Windows location for user-specific application data
        // This typically resolves to %AppData%\Roaming\WinWhisper
        _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WinWhisper"
        );

        _fullPath = Path.Combine(_directory, "settings.json");
        _settings = UserSettings.CreateDefault();
    }

    /// <summary>
    /// Loads the configuration from disk or creates a default configuration
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_fullPath))
            {
                _logger.Information($"Loading configuration from {_fullPath}");

                var json = await File.ReadAllTextAsync(_fullPath);
                var loadedConfig = UserSettings.FromJson(json);

                if (loadedConfig != null)
                {
                    _settings = loadedConfig;
                    _logger.Information("Configuration loaded successfully");
                }
                else
                {
                    _logger.Warning("Failed to parse configuration file, using defaults");
                    _settings = UserSettings.CreateDefault();
                    await SaveAsync();
                }
            }
            else
            {
                _logger.Information("Configuration file not found, creating default configuration");
                _settings = UserSettings.CreateDefault();
                await SaveAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading configuration, using defaults");
            _settings = UserSettings.CreateDefault();
        }
    }

    /// <summary>
    /// Saves the current configuration to disk
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            // Ensure the directory exists
            if (!Directory.Exists(_directory))
            {
                Directory.CreateDirectory(_directory);
                _logger.Information($"Created configuration directory: {_directory}");
            }

            var json = _settings.ToJson();
            await File.WriteAllTextAsync(_fullPath, json);

            _logger.Information($"Configuration saved to {_fullPath}");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, $"Error saving configuration to {_fullPath}");
            throw;
        }
    }

    /// <summary>
    /// Updates the configuration and saves it to disk
    /// </summary>
    public async Task UpdateConfigurationAsync(UserSettings newSettings)
    {
        _settings = newSettings;
        await SaveAsync();

        // Notify all listeners about the configuration change
        NotifyListeners();

        // Notify async listeners and wait for all to complete
        await NotifyListenersAsync();
    }

    /// <summary>
    /// Register a listener to be notified when configuration changes (IOptionsMonitor interface)
    /// </summary>
    public IDisposable OnChange(Action<UserSettings, string?> listener)
    {
        _listeners.Add(listener);
        return new ChangeToken(() => _listeners.Remove(listener));
    }

    /// <summary>
    /// Register an async listener to be notified when configuration changes
    /// UpdateConfigurationAsync will await all async listeners to complete
    /// </summary>
    public IDisposable OnChangeAsync(Func<UserSettings, string?, Task> listener)
    {
        _asyncListeners.Add(listener);
        return new ChangeToken(() => _asyncListeners.Remove(listener));
    }

    /// <summary>
    /// Gets the value for a named option (IOptionsMonitor interface implementation)
    /// </summary>
    public UserSettings Get(string? name) => _settings;

    /// <summary>
    /// Notify all registered synchronous listeners about configuration changes
    /// </summary>
    private void NotifyListeners()
    {
        foreach (var listener in _listeners.ToList())
        {
            try
            {
                listener(_settings, null);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error notifying configuration change listener");
            }
        }
    }

    /// <summary>
    /// Notify all registered async listeners about configuration changes and wait for completion
    /// </summary>
    private async Task NotifyListenersAsync()
    {
        foreach (var listener in _asyncListeners.ToList())
        {
            try
            {
                await listener(_settings, null);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error invoking async configuration change listener");
            }
        }
    }

    /// <summary>
    /// Disposable token for unregistering change listeners
    /// </summary>
    private class ChangeToken(Action onDispose) : IDisposable
    {
        private readonly Action _onDispose = onDispose;

        public void Dispose()
        {
            _onDispose();
        }
    }
}