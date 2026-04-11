using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Whisper.net.Ggml;

using WinWhisper.Common.Extensions;
using WinWhisper.Services.Abstractions;

namespace WinWhisper.Services.Transcription;

public class ModelService : IModelService
{
    private readonly ILogger<ModelService> _logger;
    private readonly string _modelDirectory;

    public ModelService(ILogger<ModelService> logger)
    {
        _logger = logger;
        _modelDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinWhisper",
            "models"
        );
    }

    public bool IsModelInstalled(GgmlType type)
    {
        var path = GetModelPath(type);
        return File.Exists(path);
    }

    public string GetModelPath(GgmlType type)
    {
        var modelName = GetModelName(type); // Reuse logic, but maybe duplicate for now or extract helper
        return Path.Combine(_modelDirectory, $"{modelName}.bin");
    }

    public async Task DownloadModelAsync(GgmlType type, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var modelName = GetModelName(type);
        var modelPath = GetModelPath(type);
        var tmpFileName = Path.GetTempFileName();

        try
        {
            if (!Directory.Exists(_modelDirectory))
            {
                Directory.CreateDirectory(_modelDirectory);
                _logger.LogInformation("Created model directory: {Path}", _modelDirectory);
            }

            var url = $"https://huggingface.co/sandrohanea/whisper.net/resolve/v3/classic/{modelName}.bin";
            _logger.LogInformation(" downloading model {Model} from {Url}", modelName, url);

            using var client = new HttpClient { Timeout = TimeSpan.FromHours(1) };

            progress?.Report(new DownloadProgress(0, 0));

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
            using (var fileWriter = File.OpenWrite(tmpFileName))
            {
                await contentStream.CopyToAsyncWithProgress(fileWriter, totalBytes, progress, cancellationToken);
            }

            // Move temp file to final location
            File.Move(tmpFileName, modelPath, true);
            _logger.LogInformation("Model {Model} downloaded successfully to {Path}", modelName, modelPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download model {Model}", modelName);
            // Clean up temp file
            if (File.Exists(tmpFileName))
            {
                try { File.Delete(tmpFileName); } catch { }
            }
            throw;
        }
    }

    public Task DeleteModelAsync(GgmlType type)
    {
        var path = GetModelPath(type);
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
                _logger.LogInformation("Deleted model {Type} at {Path}", type, path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete model {Type} at {Path}", type, path);
                throw;
            }
        }
        return Task.CompletedTask;
    }

    private static string GetModelName(GgmlType type)
    {
        return type switch
        {
            GgmlType.Tiny => "ggml-tiny",
            GgmlType.TinyEn => "ggml-tiny.en",
            GgmlType.Base => "ggml-base",
            GgmlType.BaseEn => "ggml-base.en",
            GgmlType.Small => "ggml-small",
            GgmlType.SmallEn => "ggml-small.en",
            GgmlType.Medium => "ggml-medium",
            GgmlType.MediumEn => "ggml-medium.en",
            GgmlType.LargeV1 => "ggml-large-v1",
            GgmlType.LargeV2 => "ggml-large-v2",
            GgmlType.LargeV3 => "ggml-large-v3",
            GgmlType.LargeV3Turbo => "ggml-large-v3-turbo",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }
}
