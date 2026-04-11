using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using NAudio.Wave;
using WinWhisper.Models;
using WinWhisper.Services;
using WinWhisper.Services.Abstractions;

namespace WinWhisper.ViewModels;

public class NavigationItem
{
    public string Name { get; set; } = string.Empty;
    public string IconPath { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
}

public class AudioDeviceItem
{
    public string Name { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}

public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly SettingsManager _configManager;
    private UserSettings _configuration;
    private NavigationItem _selectedNavigationItem = null!;

    public event EventHandler? RequestClose;

    public UserSettings Configuration
    {
        get => _configuration;
        set
        {
            _configuration = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; set; }

    public NavigationItem SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (_selectedNavigationItem != value)
            {
                _selectedNavigationItem = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsRecordingPageVisible));
                OnPropertyChanged(nameof(IsModelPageVisible));
                OnPropertyChanged(nameof(IsPostProcessingPageVisible));
            }
        }
    }

    public bool IsRecordingPageVisible => SelectedNavigationItem?.PageName == "RecordingPage";
    public bool IsModelPageVisible => SelectedNavigationItem?.PageName == "ModelPage";
    public bool IsPostProcessingPageVisible => SelectedNavigationItem?.PageName == "PostProcessingPage";

    public ObservableCollection<AudioDeviceItem> AudioDevices { get; } = new();

    public ModelSelectViewModel ModelSelector { get; set; }

    // Reads persisted config (not draft) and needs no PropertyChanged: the persisted
    // config is immutable for the window's lifetime, and each window open gets a fresh VM.
    public bool NeedsInitialSetup => ValidateConfiguration(_configManager.Configuration) != null;

    public ICommand ApplyCommand { get; }
    public ICommand CancelCommand { get; }

    public SettingsViewModel(SettingsManager configManager, IModelService modelService)
    {
        _configManager = configManager;
        _configuration = _configManager.Configuration.Clone();

        ModelSelector = new ModelSelectViewModel(modelService);

        if (_configuration.Model.Local.GgmlType is { } currentType)
        {
            ModelSelector.SelectModelByGgmlType(currentType);
        }

        ModelSelector.SelectedModelChanged += (s, type) =>
        {
            if (_configuration.Model.Local.GgmlType != type)
            {
                _configuration.Model.Local.GgmlType = type;
            }
        };

        LoadAudioDevices();

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new NavigationItem
            {
                Name = "Model",
                IconPath = "/Assets/Images/FluentColorBot24.png",
                PageName = "ModelPage"
            },
            new NavigationItem
            {
                Name = "Recording",
                IconPath = "/Assets/Images/FluentColorMic32.png",
                PageName = "RecordingPage"
            },
            new NavigationItem
            {
                Name = "Post-Processing",
                IconPath = "/Assets/Images/FluentColorTextBulletListSquareSparkle24.png",
                PageName = "PostProcessingPage"
            }
        };

        SelectedNavigationItem = NavigationItems.First();

        ApplyCommand = new RelayCommand(ApplySettings);
        CancelCommand = new RelayCommand(CancelSettings);
    }

    private void LoadAudioDevices()
    {
        AudioDevices.Clear();
        AudioDevices.Add(new AudioDeviceItem { Name = "(Default Device)", Id = string.Empty });

        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var deviceInfo = WaveInEvent.GetCapabilities(i);
            AudioDevices.Add(new AudioDeviceItem { Name = deviceInfo.ProductName, Id = deviceInfo.ProductName });
        }
    }

    private async Task ApplySettings()
    {
        if (ModelSelector.Models.Any(m => m.IsDownloading))
        {
            MessageBox.Show("Please wait until the model download is completed.", "Downloading", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ValidateConfiguration(Configuration) is { } error)
        {
            MessageBox.Show(error, "Invalid Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            await _configManager.UpdateConfigurationAsync(Configuration);

            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void CancelSettings()
    {
        // Validation is handled centrally by SettingsWindow's Closing handler via TryClose.
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gate shared by Cancel button and Window.Closing (X / Alt+F4).
    /// Validates the PERSISTED config (what the app reverts to if the draft is discarded),
    /// not the in-memory draft, so closing without Apply never leaves the app in a broken state.
    /// If the persisted config is invalid, the user is offered to exit the application
    /// (WinWhisper cannot function without a configured backend).
    /// </summary>
    public bool TryClose()
    {
        if (ModelSelector.Models.Any(m => m.IsDownloading))
        {
            MessageBox.Show("Please wait until the model download is completed.", "Downloading", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (ValidateConfiguration(_configManager.Configuration) is { } error)
        {
            var result = MessageBox.Show(
                $"{error}\n\nWinWhisper cannot run without this setup. Exit the application?",
                "Setup Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
                return true;
            }
            return false;
        }

        return true;
    }

    private static string? ValidateConfiguration(UserSettings config)
    {
        if (config.Model.Api.Enabled)
        {
            return string.IsNullOrWhiteSpace(config.Model.Api.ApiKey)
                ? "Please enter a valid API Key for OpenAI Whisper."
                : null;
        }

        return config.Model.Local.GgmlType == null
            ? "Please select a model to be used for local transcription."
            : null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public static class UserConfigurationExtensions
{
    public static UserSettings Clone(this UserSettings source)
    {
        var json = source.ToJson();
        return UserSettings.FromJson(json) ?? UserSettings.CreateDefault();
    }
}
