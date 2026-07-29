namespace Yoink.Shared;

/// <summary>What a pasted URL resolves to after detection/inspection.</summary>
public enum MediaSourceKind
{
    Unknown,
    Video,
    Short,
    Playlist,
    Channel,
    Livestream,
    AudioOnly,
    Unsupported
}

/// <summary>The kind of artifact a download produces.</summary>
public enum DownloadKind
{
    Video,
    Audio,
    Subtitles,
    Thumbnail
}

/// <summary>Coarse lifecycle stage reported while a download runs.</summary>
public enum DownloadStage
{
    Preparing,
    DownloadingVideo,
    DownloadingAudio,
    DownloadingSubtitles,
    DownloadingThumbnail,
    Merging,
    Converting,
    WritingMetadata,
    EmbeddingThumbnail,
    Finished,
    Error
}

/// <summary>Terminal outcome of a download attempt (mirrors the History ledger).</summary>
public enum DownloadOutcome
{
    Success,
    Failed,
    Cancelled,
    Skipped,
    Partial
}
