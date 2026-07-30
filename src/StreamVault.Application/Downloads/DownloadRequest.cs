using StreamVault.Shared;

namespace StreamVault.Application.Downloads;

/// <summary>Everything the download engine needs to build and run a yt-dlp invocation.</summary>
public sealed class DownloadRequest
{
    public required string Url { get; init; }
    public DownloadKind Kind { get; init; } = DownloadKind.Video;

    /// <summary>Explicit yt-dlp format id/selector. When null, one is derived from the fields below.</summary>
    public string? FormatId { get; init; }

    /// <summary>Target max height for video (e.g. 2160, 1080). 0 = best.</summary>
    public int MaxHeight { get; init; }

    public string VideoContainer { get; init; } = "mp4";
    public string AudioContainer { get; init; } = "m4a";
    public string? VideoCodecPreference { get; init; }
    public string AudioQuality { get; init; } = "0"; // 0 = best (yt-dlp audio-quality scale)

    public string OutputFolder { get; init; } = string.Empty;
    public string FilenameTemplate { get; init; } = "%(title)s [%(id)s].%(ext)s";

    public bool EmbedSubtitles { get; init; }
    public bool EmbedMetadata { get; init; }
    public bool EmbedThumbnail { get; init; }
    public bool WriteThumbnail { get; init; }
    public bool SplitChapters { get; init; }
    public string SubtitleLanguages { get; init; } = "en.*";

    public string? CookiesFile { get; init; }
    public string? Proxy { get; init; }
    public int SpeedLimitKbps { get; init; }

    /// <summary>Optional pre-resolved title used for history/library labelling before metadata arrives.</summary>
    public string? KnownTitle { get; init; }
}
