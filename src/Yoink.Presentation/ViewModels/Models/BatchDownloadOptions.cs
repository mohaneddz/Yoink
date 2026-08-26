namespace Yoink.Presentation.ViewModels.Models;

/// <summary>The user's choices from the "Download selected" dialog, applied to every queued upload.</summary>
public sealed record BatchDownloadOptions(
    bool IsAudio,
    string Container,
    string Quality,
    string SubtitleLanguage,
    string OutputFolder);
