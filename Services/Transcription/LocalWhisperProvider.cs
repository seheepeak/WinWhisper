using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.Logger;

using WinWhisper.Common.Extensions;
using WinWhisper.Models;
using WinWhisper.Services.Abstractions;
using WinWhisper.Views.Windows;

namespace WinWhisper.Services.Transcription;

public class LocalWhisperProvider : ITranscriptionProvider, IHostedService, IDisposable
{
    private UserSettings.ModelSettings _settings;
    private readonly ILogger<LocalWhisperProvider> _logger;
    private readonly IModelService _modelService;
    private WhisperFactory? _factory;
    private readonly IDisposable? _whisperLogger;
    private readonly IDisposable? _configSubscription;
    private volatile WhisperLogLevel _lastLogLevel = WhisperLogLevel.Info;

    public LocalWhisperProvider(SettingsManager configManager, IModelService modelService, ILogger<LocalWhisperProvider> logger)
    {
        _settings = configManager.Configuration.Model;
        _modelService = modelService;
        _logger = logger;
        _whisperLogger = LogProvider.AddLogger(OnLog);

        // Subscribe to configuration changes
        _configSubscription = configManager.OnChangeAsync(OnConfigurationChanged);
    }

    public async Task InitializeAsync()
    {
        if (GetSelectedModelType(_settings) is not GgmlType ggmlType)
        {
            _logger.LogInformation("API mode is enabled; skipping local model initialization.");
            return;
        }


        var modelPath = _modelService.GetModelPath(ggmlType);

        // Model must be downloaded via Settings UI
        if (!_modelService.IsModelInstalled(ggmlType))
        {
            _logger.LogError("Model {GgmlType} is not installed. Please download it in Settings.", ggmlType);
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                // Load model into GPU/CPU memory
                _factory = WhisperFactory.FromPath(modelPath);
                _logger.LogInformation("LocalWhisperProvider initialized with model {GgmlType}", ggmlType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize model.");
            }
        });
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return InitializeAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Implement ITranscriptionProvider: Transcribe audio stream to text
    /// </summary>
    public async Task<string> TranscribeAsync(Stream audioStream, string language, CancellationToken cancellationToken)
    {
        if (_factory == null)
        {
            _logger.LogError("WhisperFactory is not initialized.");
            return string.Empty;
        }

        using var processor = _factory.CreateBuilder().WithLanguage(language).Build();

        var finalResult = string.Empty;
        await foreach (var result in processor.ProcessAsync(audioStream, cancellationToken))
        {
            _logger.LogDebug("Transcription result: {Start}->{End}: {Text}", result.Start, result.End, result.Text);
            finalResult += result.Text;
        }
        return finalResult;
    }

    private void OnLog(WhisperLogLevel level, string? message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        if (level == WhisperLogLevel.Cont)
        {
            level = _lastLogLevel;
        }
        else
        {
            _lastLogLevel = level;
        }

        switch (level)
        {
            case WhisperLogLevel.Error:
                _logger.LogError("{Message}", message);
                break;
            case WhisperLogLevel.Warning:
                _logger.LogWarning("{Message}", message);
                break;
            case WhisperLogLevel.Info:
                _logger.LogInformation("{Message}", message);
                break;
            case WhisperLogLevel.Debug:
                _logger.LogDebug("{Message}", message);
                break;
            default:
                _logger.LogDebug("[Unknown:{Level}] {Message}", level, message);
                break;
        }
    }

    /// <summary>
    /// Handle configuration changes - reinitialize if model settings changed
    /// </summary>
    private async Task OnConfigurationChanged(UserSettings newConfig, string? name)
    {
        var oldModelType = _factory != null ? GetSelectedModelType(_settings) : null;
        var newModelType = GetSelectedModelType(newConfig.Model);
        _settings = newConfig.Model;

        if (oldModelType != newModelType)
        {
            _logger.LogInformation("Model type changed from {OldType} to {NewType}. Reinitializing LocalWhisperProvider.",
                oldModelType?.ToString() ?? "API", newModelType?.ToString() ?? "API");

            // Dispose old factory and reinitialize
            _factory?.Dispose();
            _factory = null;

            if (newModelType.HasValue)
            {
                await InitializeAsync();
            }
        }
    }

    public void Dispose()
    {
        _configSubscription?.Dispose();
        _factory?.Dispose();
        _whisperLogger?.Dispose();
        GC.SuppressFinalize(this);
    }

    private static GgmlType? GetSelectedModelType(UserSettings.ModelSettings settings)
    {
        return settings.Api.Enabled ? null : settings.Local.GgmlType;
    }
}
