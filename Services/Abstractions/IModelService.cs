using System;
using System.Threading;
using System.Threading.Tasks;

using Whisper.net.Ggml;

using WinWhisper.Common.Extensions;

namespace WinWhisper.Services.Abstractions;

public interface IModelService
{
    bool IsModelInstalled(GgmlType type);
    string GetModelPath(GgmlType type);
    Task DownloadModelAsync(GgmlType type, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
    Task DeleteModelAsync(GgmlType type);
}
