using Yoink.Domain.Media;
using Yoink.Shared;

namespace Yoink.Application.Abstractions;

/// <summary>Inspects URLs via yt-dlp to return media/playlist metadata and available formats.</summary>
public interface IMediaInspector
{
    /// <summary>Quick, offline guess of what a URL points at (no network).</summary>
    MediaSourceKind DetectKind(string url);

    /// <summary>Full single-item inspection (metadata + formats).</summary>
    Task<Result<MediaMetadata>> InspectAsync(string url, CancellationToken ct = default);

    /// <summary>Flat playlist/channel listing (entries with basic info, fast).</summary>
    Task<Result<PlaylistMetadata>> InspectPlaylistAsync(string url, int? limit = null, CancellationToken ct = default);
}
