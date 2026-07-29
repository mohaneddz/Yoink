using Yoink.Shared;

namespace Yoink.Domain.Entities;

/// <summary>
/// Persisted snapshot of a Queue page job, so the queue survives an app restart (spec §7.2, §20).
/// <see cref="RequestJson"/> is the serialized <c>DownloadRequest</c> needed to resume/retry the job.
/// </summary>
public sealed class QueueJobEntity
{
    public int Id { get; set; }
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string SizeText { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
    public string SelectedFormatText { get; set; } = string.Empty;
    public string ThumbnailHex { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string FileSizeText { get; set; } = string.Empty;
    public string DownloadedText { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string StatusDetailText { get; set; } = string.Empty;
    public double Progress { get; set; }
    public int RetryCount { get; set; }
    public PersistedQueueStatus Status { get; set; }
    public DownloadKind Kind { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastUpdateAt { get; set; }

    /// <summary>JSON-serialized <c>Yoink.Application.Downloads.DownloadRequest</c>.</summary>
    public string RequestJson { get; set; } = string.Empty;
}

/// <summary>Mirrors <c>QueueJobStatus</c> in the Presentation layer so Domain has no upward reference.</summary>
public enum PersistedQueueStatus
{
    Inspecting,
    Waiting,
    Queued,
    DownloadingVideo,
    DownloadingAudio,
    DownloadingSubtitles,
    Merging,
    Converting,
    WritingMetadata,
    Completed,
    Paused,
    Cancelled,
    Failed,
    Retrying
}
