using System;
using System.ClientModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using OpenAI;
using OpenAI.Audio;

using WinWhisper.Models;
using WinWhisper.Services.Abstractions;

namespace WinWhisper.Services.Transcription;

/// <summary>
/// Transcription provider using OpenAI Whisper API
/// </summary>
public class OpenAIWhisperProvider : ITranscriptionProvider, IDisposable
{
    private UserSettings.ModelSettings.ApiSettings _settings;
    private readonly ILogger<OpenAIWhisperProvider> _logger;
    private readonly IDisposable? _configSubscription;
    private OpenAIClient _client;

    public OpenAIWhisperProvider(IOptionsMonitor<UserSettings> configMonitor, ILogger<OpenAIWhisperProvider> logger)
    {
        _settings = configMonitor.CurrentValue.Model.Api;
        _logger = logger;

        // Initialize OpenAI client
        _client = CreateClient(_settings);

        // Subscribe to configuration changes
        _configSubscription = configMonitor.OnChange(OnConfigurationChanged);
    }

    private static OpenAIClient CreateClient(UserSettings.ModelSettings.ApiSettings settings)
    {
        var options = string.IsNullOrEmpty(settings.BaseUrl)
            ? null
            : new OpenAIClientOptions { Endpoint = new Uri(settings.BaseUrl) };

        return new OpenAIClient(new ApiKeyCredential(settings.ApiKey), options);
    }

    /// <summary>
    /// Implementation of ITranscriptionProvider interface: Converts audio to text using OpenAI API
    /// </summary>
    public async Task<string> TranscribeAsync(Stream audioStream, string language, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Starting OpenAI transcription with model {Model}, language {Language}", _settings.Model, language);

            // Request transcription using OpenAI SDK
            var audioClient = _client.GetAudioClient(_settings.Model);

            var options = new AudioTranscriptionOptions
            {
                Language = language,
                ResponseFormat = AudioTranscriptionFormat.Text
            };

            // TODO
            if (language == "en")
            {
                options.Prompt = "The attached audio conversation (for recitation into text) takes place solely in the English language.";
            }

            var result = await audioClient.TranscribeAudioAsync(audioStream, "audio.wav", options, cancellationToken);

            var transcription = result.Value.Text;
            _logger.LogInformation("OpenAI transcription completed: {Length} characters", transcription.Length);

            return transcription;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI transcription failed");
            throw;
        }
    }

    /// <summary>
    /// Handle configuration changes - recreate client if API settings changed
    /// </summary>
    private void OnConfigurationChanged(UserSettings newConfig, string? name)
    {
        var oldSettings = _settings;
        var newSettings = newConfig.Model.Api;
        _settings = newSettings;

        if (newSettings != oldSettings)
        {
            _client = CreateClient(newSettings);
            _logger.LogInformation("API configuration changed. OpenAI client recreated.");
        }
    }

    public void Dispose()
    {
        _configSubscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}
