using System;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using WinWhisper.Services.Abstractions;

namespace WinWhisper.Services.Transcription;

/// <summary>
/// Factory that selects the appropriate transcription provider based on user settings.
/// </summary>
public class TranscriptionProviderFactory
{
    private readonly SettingsManager _configService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TranscriptionProviderFactory> _logger;

    public TranscriptionProviderFactory(
        SettingsManager configService,
        IServiceProvider serviceProvider,
        ILogger<TranscriptionProviderFactory> logger)
    {
        _configService = configService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Returns the provider that matches the current settings.
    /// </summary>
    public ITranscriptionProvider GetProvider()
    {
        var config = _configService.Configuration;

        // Use OpenAI when the API is enabled and a key is configured.
        if (config.Model.Api.Enabled && !string.IsNullOrEmpty(config.Model.Api.ApiKey))
        {
            _logger.LogInformation("Using OpenAI Whisper API for transcription");
            return _serviceProvider.GetRequiredService<OpenAIWhisperProvider>();
        }
        else
        {
            _logger.LogInformation("Using local Whisper model for transcription");
            return _serviceProvider.GetRequiredService<LocalWhisperProvider>();
        }
    }
}
