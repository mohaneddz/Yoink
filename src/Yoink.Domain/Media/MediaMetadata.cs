using Yoink.Shared;

namespace Yoink.Domain.Media;

/// <summary>Full metadata for a single media item as returned by yt-dlp <c>--dump-single-json</c>.</summary>
public sealed class MediaMetadata
{
    public required string Id { get; init; }
    public required string WebpageUrl { get; init; }
    public string? OriginalUrl { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? Channel { get; init; }
    public string? ChannelId { get; init; }
    public string? ChannelUrl { get; init; }
    public string? Uploader { get; init; }
    public long? ChannelFollowerCount { get; init; }
    public double? DurationSeconds { get; init; }
    public long? ViewCount { get; init; }
    public long? LikeCount { get; init; }
    public DateOnly? UploadDate { get; init; }
    public string? ThumbnailUrl { get; init; }
    public bool IsLive { get; init; }
    public bool WasLive { get; init; }
    public string? Availability { get; init; }
    public string? Extractor { get; init; }
    public MediaSourceKind SourceKind { get; init; }

    public IReadOnlyList<MediaFormat> Formats { get; init; } = Array.Empty<MediaFormat>();

    /// <summary>Subtitle language codes available (manual + automatic captions).</summary>
    public IReadOnlyList<string> SubtitleLanguages { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AutomaticCaptionLanguages { get; init; } = Array.Empty<string>();
    public int ChapterCount { get; init; }

    public TimeSpan? Duration => DurationSeconds is { } s ? TimeSpan.FromSeconds(s) : null;
}
