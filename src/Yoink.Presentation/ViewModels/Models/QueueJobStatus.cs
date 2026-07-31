namespace Yoink.Presentation.ViewModels.Models;

public enum QueueJobStatus
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
