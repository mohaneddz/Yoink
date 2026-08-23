using Yoink.Shared;

namespace Yoink.Domain.Media;

/// <summary>A lightweight playlist/channel listing (flat extraction: entries carry basic info only).</summary>
public sealed class PlaylistMetadata
{
    public required string Id { get; init; }
    public required string WebpageUrl { get; init; }
    public required string Title { get; init; }
    public string? Uploader { get; init; }
    public string? ChannelId { get; init; }
    public string? Description { get; init; }
    public string? Availability { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? ChannelUrl { get; init; }
    public long? SubscriberCount { get; init; }
    public MediaSourceKind SourceKind { get; init; }
    public IReadOnlyList<PlaylistEntry> Entries { get; init; } = Array.Empty<PlaylistEntry>();
}

/// <summary>A single entry inside a <see cref="PlaylistMetadata"/> (flat listing).</summary>
public sealed class PlaylistEntry
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required string Title { get; init; }
    public double? DurationSeconds { get; init; }
    public string? Uploader { get; init; }
    public long? ViewCount { get; init; }
    public string? ThumbnailUrl { get; init; }
    public DateOnly? UploadDate { get; init; }
    public string Availability { get; init; } = "available";

    public TimeSpan? Duration => DurationSeconds is { } s ? TimeSpan.FromSeconds(s) : null;
}
