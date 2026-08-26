using CommunityToolkit.Mvvm.ComponentModel;
using Yoink.Presentation.ViewModels.Models;

namespace Yoink.Presentation.ViewModels;

/// <summary>Backs the editable "Download selected" dialog: video/audio mode, format, quality, subtitles, and save folder.</summary>
public partial class BatchDownloadDialogViewModel : ObservableObject
{
    private static readonly string[] VideoContainers = { "MP4", "MKV", "WEBM" };
    private static readonly string[] AudioContainers = { "MP3", "M4A", "OPUS", "FLAC", "WAV", "AAC" };
    private static readonly string[] VideoQualities = { "Best available", "2160p", "1440p", "1080p", "720p", "480p", "360p" };
    private static readonly string[] AudioQualities = { "Best", "320 kbps", "256 kbps", "192 kbps", "128 kbps", "96 kbps" };

    public BatchDownloadDialogViewModel(int count, string container, string outputFolder)
    {
        HeadlineText = count == 1 ? "DOWNLOAD 1 VIDEO" : $"DOWNLOAD {count} VIDEOS";
        _container = VideoContainers.FirstOrDefault(c => string.Equals(c, container, StringComparison.OrdinalIgnoreCase)) ?? "MP4";
        _outputFolder = outputFolder;
    }

    public string HeadlineText { get; }

    public string[] FormatOptions => IsAudio ? AudioContainers : VideoContainers;
    public string[] QualityOptions => IsAudio ? AudioQualities : VideoQualities;
    public string QualityLabel => IsAudio ? "AUDIO QUALITY" : "QUALITY";
    public bool ShowSubtitles => !IsAudio;

    public string[] SubtitleOptions { get; } =
        { "None", "English", "Spanish", "French", "German", "Japanese", "All" };

    [ObservableProperty] private bool _isAudio;
    [ObservableProperty] private string _container = "MP4";
    [ObservableProperty] private string _quality = "Best available";
    [ObservableProperty] private string _subtitleLanguage = "None";
    [ObservableProperty] private string _outputFolder = string.Empty;

    /// <summary>Mirror of <see cref="IsAudio"/> for the "Video" radio button.</summary>
    public bool IsVideo
    {
        get => !IsAudio;
        set { if (value) IsAudio = false; }
    }

    partial void OnIsAudioChanged(bool value)
    {
        OnPropertyChanged(nameof(IsVideo));
        OnPropertyChanged(nameof(FormatOptions));
        OnPropertyChanged(nameof(QualityOptions));
        OnPropertyChanged(nameof(QualityLabel));
        OnPropertyChanged(nameof(ShowSubtitles));
        Container = FormatOptions.First();
        Quality = QualityOptions.First();
    }

    public BatchDownloadOptions ToOptions() => new(IsAudio, Container, Quality, SubtitleLanguage, OutputFolder);
}
