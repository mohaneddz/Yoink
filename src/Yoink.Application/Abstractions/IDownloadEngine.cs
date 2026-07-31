using Yoink.Application.Downloads;

namespace Yoink.Application.Abstractions;

/// <summary>Runs a single download to completion, reporting live progress.</summary>
public interface IDownloadEngine
{
    Task<DownloadResult> DownloadAsync(
        DownloadRequest request,
        IProgress<DownloadProgress> progress,
        CancellationToken ct);
}
