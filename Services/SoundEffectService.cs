using System.Collections.Concurrent;
using System.IO;

using Microsoft.Extensions.Hosting;

using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace WinWhisper.Services;

public sealed class SoundEffectService : IHostedService, IDisposable
{
    public static SoundEffectService Instance { get; private set; } = null!;

    private MMDevice? _device;
    private IWavePlayer? _output;
    private MixingSampleProvider? _mixer;
    private int _sampleRate;
    private int _channels;
    private readonly ConcurrentDictionary<string, CachedSound> _cache = new();

    public SoundEffectService()
    {
        Instance = this;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Get default output device and match mix format (typically 48000 Hz, 2ch, float)
        _device = new MMDeviceEnumerator()
            .GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

        _sampleRate = _device.AudioClient.MixFormat.SampleRate;
        _channels = Math.Min(2, _device.AudioClient.MixFormat.Channels);

        _mixer = new MixingSampleProvider(
            WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, _channels))
        {
            // Keep filling with zeros when no input → keeps device "always on"
            ReadFully = true
        };

        // Low-latency WASAPI (event-driven). Fallback to WaveOutEvent on failure
        try
        {
            _output = new WasapiOut(_device, AudioClientShareMode.Shared,
                                    useEventSync: true, latency: 200);
        }
        catch
        {
            var waveOut = new WaveOutEvent
            {
                DesiredLatency = 200,
                NumberOfBuffers = 2
            };
            _output = waveOut;
        }

        _output.Init(_mixer);
        _output.Play(); // Always playing (silent stream)

        // Preload system sounds from disk
        await Task.Run(() =>
        {
            var mediaPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media");
            Load("Speech On", Path.Combine(mediaPath, "Speech On.wav"));
            Load("Speech Off", Path.Combine(mediaPath, "Speech Off.wav"));
        }, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Load sound file into memory at startup or when needed
    /// </summary>
    public void Load(string key, string path)
        => _cache[key] = new CachedSound(path, _sampleRate, _channels);

    /// <summary>
    /// Supports overlapping playback, non-blocking
    /// </summary>
    public void Play(string key, float volume = 1f)
    {
        if (!_cache.TryGetValue(key, out var sound))
            return;

        var src = new CachedSoundSampleProvider(sound);
        ISampleProvider provider = volume >= 0.999f
            ? src
            : new VolumeSampleProvider(src) { Volume = Math.Clamp(volume, 0f, 1f) };

        _mixer?.AddMixerInput(provider); // Multiple calls will be mixed together
        // Mixer automatically removes provider when it returns 0 (playback complete)
    }

    public void Dispose()
    {
        _output?.Stop();
        _output?.Dispose();
        _device?.Dispose();
    }
}

/// <summary>
/// Caches sound effect in engine format (sample rate/channels) in memory
/// </summary>
public sealed class CachedSound
{
    public float[] AudioData { get; }
    public WaveFormat WaveFormat { get; }

    public CachedSound(string fileName, int targetSampleRate, int targetChannels)
    {
        using var reader = new AudioFileReader(fileName); // float 32
        ISampleProvider sp = reader;

        // Convert to engine format (resample/channel conversion)
        if (reader.WaveFormat.SampleRate != targetSampleRate)
            sp = new WdlResamplingSampleProvider(sp, targetSampleRate);

        if (reader.WaveFormat.Channels != targetChannels)
            sp = targetChannels == 1
                ? new StereoToMonoSampleProvider(sp)
                : new MonoToStereoSampleProvider(sp);

        var list = new List<float>(capacity: (int)(reader.TotalTime.TotalSeconds * targetSampleRate * targetChannels) + 1);
        var buffer = new float[targetSampleRate * targetChannels / 10]; // ≈100ms buffer chunks
        int read;
        while ((read = sp.Read(buffer, 0, buffer.Length)) > 0)
            list.AddRange(buffer.AsSpan(0, read).ToArray());

        AudioData = list.ToArray();
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(targetSampleRate, targetChannels);
    }
}

/// <summary>
/// Wraps cached buffer as ISampleProvider for mixer input
/// </summary>
public sealed class CachedSoundSampleProvider(CachedSound sound) : ISampleProvider
{
    private readonly CachedSound _sound = sound;
    private long _position;

    public WaveFormat WaveFormat => _sound.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        long remaining = _sound.AudioData.Length - _position;
        if (remaining <= 0) return 0;

        int toCopy = (int)Math.Min(remaining, count);
        Array.Copy(_sound.AudioData, _position, buffer, offset, toCopy);
        _position += toCopy;
        return toCopy;
    }
}
