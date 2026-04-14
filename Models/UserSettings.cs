using System.Diagnostics;
using System.Security.Policy;
using System.Text.Json;
using System.Text.Json.Serialization;

using Whisper.net.Ggml;

namespace WinWhisper.Models;

/// <summary>
/// General user configuration for the WinWhisper application
/// </summary>
public class UserSettings
{
    [JsonPropertyName("model")]
    public ModelSettings Model { get; set; } = new ModelSettings();

    [JsonPropertyName("recording")]
    public RecordingSettings Recording { get; set; } = new RecordingSettings();

    [JsonPropertyName("postProcessing")]
    public PostProcessingSettings PostProcessing { get; set; } = new PostProcessingSettings();

    public class ModelSettings
    {
        public record ApiSettings
        {
            /// <summary>
            /// Whether to use the OpenAI API (false uses the local model).
            /// </summary>
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; } = false;

            /// <summary>
            /// OpenAI API key.
            /// </summary>
            [JsonPropertyName("apiKey")]
            public string ApiKey { get; set; } = string.Empty;

            /// <summary>
            /// Custom API endpoint (default: empty, uses the official OpenAI endpoint).
            /// </summary>
            [JsonPropertyName("baseUrl")]
            public string BaseUrl { get; set; } = string.Empty;

            /// <summary>
            /// Model name to use (default: "gpt-4o-transcribe").
            /// </summary>
            [JsonPropertyName("model")]
            public string Model { get; set; } = "gpt-4o-transcribe";
        }

        public record LocalSettings
        {
            /// <summary>
            /// Selected local Whisper model.
            /// null means the user has never chosen a local model (fresh install / API-only user).
            /// Once set, it represents the user's "sticky" local model that must remain on disk.
            /// </summary>
            [JsonPropertyName("ggmlType")]
            public GgmlType? GgmlType { get; set; } = null;
        }

        [JsonPropertyName("api")]
        public ApiSettings Api { get; set; } = new ApiSettings();

        [JsonPropertyName("local")]
        public LocalSettings Local { get; set; } = new LocalSettings();
    }

    /// <summary>
    /// Recording configuration settings
    /// </summary>
    public record RecordingSettings
    {
        /// <summary>
        /// Activation key to start/stop recording (default: "win+~")
        /// </summary>
        [JsonPropertyName("activationKey")]
        public string ActivationKey { get; set; } = "win+~";

        /// <summary>
        /// Milliseconds of continuous silence before stopping recording
        /// </summary>
        [JsonPropertyName("silenceDuration")]
        public int SilenceDuration { get; set; } = 900;

        /// <summary>
        /// Minimum recording duration in milliseconds (recordings shorter than this are discarded)
        /// </summary>
        [JsonPropertyName("minDuration")]
        public int MinDuration { get; set; } = 100;

        /// <summary>
        /// Recording mode. See <see cref="Models.RecordingMode"/> for available values.
        /// </summary>
        [JsonPropertyName("recordingMode")]
        public RecordingMode RecordingMode { get; set; } = RecordingMode.PressToToggle;

        /// <summary>
        /// Name of the recording device to use (empty string for default device)
        /// </summary>
        /// [JsonPropertyName("deviceName")]
        public string DeviceName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Post-processing configuration settings for transcription results
    /// </summary>
    public record PostProcessingSettings
    {
        /// <summary>
        /// Whether to trim leading and trailing whitespace from transcription results
        /// </summary>
        [JsonPropertyName("trimWhitespace")]
        public bool TrimWhitespace { get; set; } = true;

        /// <summary>
        /// Delay in milliseconds between simulated key presses when typing transcription results
        /// </summary>
        [JsonPropertyName("writingKeyPressDelayMs")]
        public int WritingKeyPressDelayMs { get; set; } = 5;

        /// <summary>
        /// Input method used when delivering transcribed text. See <see cref="Models.InputMethod"/>.
        /// </summary>
        [JsonPropertyName("inputMethod")]
        public InputMethod InputMethod { get; set; } = InputMethod.Keyboard;
    }

    /// <summary>
    /// Creates a default configuration with standard settings
    /// </summary>
    public static UserSettings CreateDefault()
    {
        return new UserSettings();
    }

    /// <summary>
    /// Serializes the configuration to JSON string
    /// </summary>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Deserializes configuration from JSON string
    /// </summary>
    public static UserSettings? FromJson(string json)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
            return JsonSerializer.Deserialize<UserSettings>(json, options);
        }
        catch (Exception e)
        {
            Debug.WriteLine("Failed to deserialize UserConfiguration: {Message}", e.Message);
            return null;
        }
    }
}