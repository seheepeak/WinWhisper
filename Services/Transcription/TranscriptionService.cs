using System.Diagnostics;
using System.IO;

using Microsoft.Extensions.Logging;

using NAudio.Utils;
using NAudio.Wave;

using WebRtcVadSharp;

using WinWhisper.Models;

using static Windows.Win32.PInvoke;
using WinWhisper.Services.Abstractions;

using static WinWhisper.Common.Threading.DispatcherHelper;

using Const = WinWhisper.Common.Constants;

namespace WinWhisper.Services.Transcription;

public enum TranscriptionEventType
{
    RecordingStarted,
    LanguageChanged,
    VoiceDetected,
    TranscribingStarted,
    TranscribingCompleted,
    Finished
}

public class TranscriptionEventArgs : EventArgs
{
    public TranscriptionEventType Type { get; }
    public object? Payload { get; }

    public TranscriptionEventArgs(TranscriptionEventType type, object? payload = null)
    {
        Type = type;
        Payload = payload;
    }
}

public sealed class TranscriptionService : IDisposable, IAsyncDisposable
{
    public event EventHandler<TranscriptionEventArgs>? TranscriptionEvent;
    private readonly TranscriptionProviderFactory _providerFactory;
    private readonly UserSettings _config;
    private readonly ILogger<TranscriptionService> _logger;
    private readonly LastTranscriptionStore _lastTranscriptionStore;
    private readonly CancellationTokenSource _recordingCts = new(); // just recording stopped
    private readonly CancellationTokenSource _serviceCts = new(); // whole service cancelled
    private Task? _task;
    private string _langCode = "en";

    private bool _disposed;

    public TranscriptionService(
        TranscriptionProviderFactory providerFactory,
        UserSettings config,
        ILogger<TranscriptionService> logger,
        LastTranscriptionStore lastTranscriptionStore)
    {
        _providerFactory = providerFactory;
        _config = config;
        _logger = logger;
        _lastTranscriptionStore = lastTranscriptionStore;
    }

    public void Start()
    {
        Debug.Assert(_task == null);
        _task = Task.Run(RunAsync, _serviceCts.Token);
    }

    public Task Stop(bool aborted = false)
    {
        var cts = aborted ? _serviceCts : _recordingCts;
        cts.Cancel();
        return _task ?? Task.CompletedTask;
    }

    private async Task RunAsync()
    {
        string? errorMessage = null;
        try
        {
            while (!_serviceCts.IsCancellationRequested)
            {
                var recordingDevice = GetRecordingDevice();
                if (recordingDevice < 0)
                {
                    errorMessage = "No recording devices available.";
                    return;
                }

                _langCode = GetInputLanguage() ?? "en";
                TranscriptionEvent?.Invoke(this, new TranscriptionEventArgs(TranscriptionEventType.RecordingStarted, _langCode));

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_serviceCts.Token, _recordingCts.Token);
                using var wavStream = await GetRecordingAsync(recordingDevice, linkedCts.Token);
                if (_serviceCts.IsCancellationRequested)
                    return;

                var transcription = string.Empty;
                if (wavStream.Length > 0)
                {
                    TranscriptionEvent?.Invoke(this, new TranscriptionEventArgs(TranscriptionEventType.TranscribingStarted));

                    wavStream.Seek(0, SeekOrigin.Begin);

                    var provider = _providerFactory.GetProvider();
                    transcription = await provider.TranscribeAsync(wavStream, _langCode, _serviceCts.Token);

                    transcription = PostProcess(transcription);
                }

                if (!_serviceCts.IsCancellationRequested && transcription.Length > 0)
                {
                    _lastTranscriptionStore.Set(transcription);
                    TranscriptionEvent?.Invoke(this, new TranscriptionEventArgs(TranscriptionEventType.TranscribingCompleted, transcription));
                }

                if (_config.Recording.RecordingMode != "continuous")
                    return;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transcription run failed");
            errorMessage = ex.Message;
        }
        finally
        {
            TranscriptionEvent?.Invoke(this, new TranscriptionEventArgs(TranscriptionEventType.Finished, errorMessage));
        }
    }

    private async Task<MemoryStream> GetRecordingAsync(int deviceNumber, CancellationToken ct)
    {
        // Setup audio recording
        var streamLock = new object();
        var wavFormat = new WaveFormat(
            Const.AudioConstants.MicSampleRate,
            Const.AudioConstants.MicBitsPerSample,
            Const.AudioConstants.MicChannels
        );
        var wavStream = new MemoryStream(4096);
        using var writer = new WaveFileWriter(new IgnoreDisposeStream(wavStream), wavFormat);
        using var waveIn = new WaveInEvent
        {
            WaveFormat = wavFormat,
            BufferMilliseconds = 100,
            DeviceNumber = deviceNumber
        };
        var streamOffset = wavStream.Length;

        // Create VAD
        using var vad = new WebRtcVad
        {
            FrameLength = FrameLength.Is30ms,
            SampleRate = SampleRate.Is16kHz,
            OperatingMode = OperatingMode.VeryAggressive
        };

        var frameDurationMs = Const.AudioConstants.VADFrameDurationMs;
        var frameSize = wavFormat.AverageBytesPerSecond * frameDurationMs / 1000;
        // 150ms delay before starting VAD to avoid mistaking the sound of key pressing for voice
        var initialFramesToSkip = 150 / frameDurationMs;
        var silentFramesThresh = _config.Recording.SilenceDuration / frameDurationMs;
        var hasSpeechDetected = false;
        var silentFrameCount = 0;

        waveIn.DataAvailable += (sender, e) =>
        {
            if (e.BytesRecorded <= 0)
                return;

            lock (streamLock)
            {
                writer.Write(e.Buffer, 0, e.BytesRecorded);
            }
        };

        var stoppedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        waveIn.RecordingStopped += (sender, e) => stoppedTcs.SetResult(true);

        try
        {
            waveIn.StartRecording();
            var stopwatch = Stopwatch.StartNew();
            var frameBuffer = new byte[frameSize];

            while (!ct.IsCancellationRequested)
            {
                // Monitor input language change
                if (stopwatch.ElapsedMilliseconds > 100)
                {
                    var currentLangCode = GetInputLanguage() ?? "en";
                    if (currentLangCode != _langCode)
                    {
                        _langCode = currentLangCode;
                        TranscriptionEvent?.Invoke(this, new TranscriptionEventArgs(TranscriptionEventType.LanguageChanged, _langCode));
                    }
                    stopwatch.Restart();
                }

                // pop next frame for vad
                var hasNewFrame = false;
                lock (streamLock)
                {
                    var availableBytes = wavStream.Length - streamOffset;
                    if (availableBytes >= frameSize)
                    {
                        var streamBuffer = wavStream.GetBuffer();
                        Array.Copy(streamBuffer, streamOffset, frameBuffer, 0, frameSize);
                        streamOffset += frameSize;
                        hasNewFrame = true;
                    }
                }

                if (!hasNewFrame)
                {
                    await Task.Delay(frameDurationMs, ct);
                    continue;
                }

                bool hasSpeech = vad.HasSpeech(frameBuffer);
                if (hasSpeech)
                {
                    TranscriptionEvent?.Invoke(this, new TranscriptionEventArgs(TranscriptionEventType.VoiceDetected));
                }

                if (initialFramesToSkip > 0)
                {
                    initialFramesToSkip -= 1;
                    continue;
                }

                // Silence detection for VAD and continuous modes
                if (_config.Recording.RecordingMode is "voice_activity_detection" or "continuous")
                {
                    if (hasSpeech)
                    {
                        hasSpeechDetected = true;
                        silentFrameCount = 0;
                    }
                    else if (hasSpeechDetected)
                    {
                        silentFrameCount += 1;
                    }

                    if (hasSpeechDetected && silentFrameCount > silentFramesThresh)
                    {
                        break;
                    }
                }
            }
        }
        catch (OperationCanceledException) { }

        try { waveIn.StopRecording(); } catch { }
        await stoppedTcs.Task;

        // Check minimum duration
        var recordingDuration = writer.TotalTime.TotalMilliseconds;
        if (recordingDuration < _config.Recording.MinDuration)
        {
            wavStream.Dispose();
            return new MemoryStream();
        }

        return wavStream;
    }

    private string PostProcess(string transcription)
    {
        if (_config.PostProcessing.TrimWhitespace)
        {
            transcription = transcription.Trim();
        }
        return transcription;
    }

    private int GetRecordingDevice()
    {
        if (WaveIn.DeviceCount == 0)
            return -1;

        var deviceName = _config.Recording.DeviceName;
        if (string.IsNullOrEmpty(deviceName))
            return 0; // Default device

        for (int i = 0; i < WaveIn.DeviceCount; i++)
        {
            var capabilities = WaveIn.GetCapabilities(i);
            if (capabilities.ProductName.Contains(deviceName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0; // Default device if not found
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        AssertMainThread();
        _disposed = true;

        try { await Stop(true).ConfigureAwait(false); }
        catch (Exception) { }
        _serviceCts.Dispose();
        _recordingCts.Dispose();
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public static string? GetInputLanguage()
    {
        // Get the window that currently has the keyboard focus
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == default)
            return null;

        // Get the identifier of the thread that created the window
        uint threadId;
        unsafe
        {
            threadId = GetWindowThreadProcessId(foregroundWindow, null);
        }
        if (threadId == 0)
            return null;

        // Get the current keyboard layout for the thread
        var layoutId = GetKeyboardLayout(threadId);

        // Extract the language ID from the layout ID
        ushort languageId = (ushort)((nint)layoutId & 0xFFFF);

        var idToCode = Const.LanguageMappings.LangIdToCode;
        return idToCode.TryGetValue(languageId, out var language) ? language : null;
    }
}