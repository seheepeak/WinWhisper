using System.IO;

namespace WinWhisper.Services.Abstractions;

/// <summary>
/// Interface for speech-to-text transcription providers
/// </summary>
public interface ITranscriptionProvider
{
    /// <summary>
    /// Transcribes audio stream to text
    /// </summary>
    /// <param name="audioStream">Audio stream in WAV format (16kHz, 16-bit, mono)</param>
    /// <param name="language">Language code (e.g., "ko", "en")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transcribed text</returns>
    Task<string> TranscribeAsync(Stream audioStream, string language, CancellationToken cancellationToken);
}
