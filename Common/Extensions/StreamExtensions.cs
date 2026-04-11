using System.Diagnostics;
using System.IO;

namespace WinWhisper.Common.Extensions;

public static class StreamExtensions
{
    private const int DefaultCopyBufferSize = 81920; // .NET standard buffer size (80KB)

    public static async Task CopyToAsyncWithProgress(
        this Stream source,
        Stream destination,
        long? totalBytes,
        IProgress<DownloadProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var buffer = new byte[DefaultCopyBufferSize];
        var totalRead = 0L;
        int bytesRead;
        var stopwatch = Stopwatch.StartNew();

        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalRead += bytesRead;
            if (stopwatch.ElapsedMilliseconds > 200)
            {
                progress?.Report(new DownloadProgress(totalRead, totalBytes ?? -1));
                stopwatch.Restart();
            }
        }

        // Final report
        progress?.Report(new DownloadProgress(totalRead, totalBytes ?? totalRead));
    }
}

public record DownloadProgress(long BytesDownloaded, long TotalBytes)
{
    public double ProgressPercentage => TotalBytes > 0
        ? (double)BytesDownloaded / TotalBytes * 100.0
        : 0;
}
