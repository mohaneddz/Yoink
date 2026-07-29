using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVault.Presentation.ViewModels.Models;

namespace StreamVault.Presentation.ViewModels;

public enum DownloadMode
{
    Video,
    Audio,
    Subtitles,
    Thumbnail
}

/// <summary>Backs the Download page (spec section 7.1): paste, inspect, configure, and start a download.</summary>
public partial class DownloadViewModel : ObservableObject
{
    public DownloadViewModel()
    {
        LoadSampleInspection();
    }

    // ---- Source bar ----
    [ObservableProperty]
    private string _sourceUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

    [ObservableProperty]
    private DetectedSourceType _detectedType = DetectedSourceType.Video;

    [ObservableProperty]
    private bool _hasInspectedMedia = true;

    // ---- Media preview ----
    [ObservableProperty] private string _title = "Switzerland 4K - Scenic Relaxation Film with Calming Music";
    [ObservableProperty] private string _channelName = "Scenic Relaxation";
    [ObservableProperty] private bool _channelVerified = true;
    [ObservableProperty] private string _durationText = "12:47";
    [ObservableProperty] private string _viewCountText = "26.4M views";
    [ObservableProperty] private string _uploadedAgoText = "2 years ago";
    [ObservableProperty] private string _uploadDateText = "May 10, 2023";
    [ObservableProperty] private string _viewCountFullText = "26,439,732";
    [ObservableProperty] private string _likeCountText = "312,521";
    [ObservableProperty] private string _descriptionPreview =
        "Experience the breathtaking beauty of Switzerland in stunning 4K. Enjoy scenic landscapes, relaxing music, and peaceful natural...";
    [ObservableProperty] private string _subtitleAvailabilityText = "Subtitles available (12)";

    // ---- Download mode tabs ----
    [ObservableProperty]
    private DownloadMode _selectedMode = DownloadMode.Video;

    [RelayCommand]
    private void SelectMode(DownloadMode mode) => SelectedMode = mode;

    // ---- Video controls ----
    [ObservableProperty] private string _quality = "Best Quality (4K)";
    [ObservableProperty] private string _container = "MP4";
    [ObservableProperty] private string _videoCodec = "H.264 (AVC)";
    [ObservableProperty] private string _audioQuality = "320 kbps";
    [ObservableProperty] private string _subtitleLanguage = "English (auto)";
    [ObservableProperty] private bool _embedSubtitles = true;
    [ObservableProperty] private bool _embedMetadata = true;
    [ObservableProperty] private bool _embedThumbnail = true;
    [ObservableProperty] private string _chapters = "All Chapters";
    [ObservableProperty] private string _outputFolder = @"C:\Users\Alex\Videos\StreamVault";
    [ObservableProperty] private string _fileNameTemplate = "%(title)s - %(quality)s - %(channel)s";

    // ---- Available streams ----
    public ObservableCollection<StreamOption> Streams { get; } = new();

    [ObservableProperty]
    private StreamOption? _selectedStream;

    public string StreamCountText => $"{Streams.Count} streams available";

    public string SelectedStreamText => SelectedStream is null ? "No stream selected" : $"Selected: {SelectedStream.Resolution}";

    partial void OnSelectedStreamChanged(StreamOption? value) => OnPropertyChanged(nameof(SelectedStreamText));

    [RelayCommand]
    private void RefreshStreams()
    {
        // Placeholder until milestone M4 wires the real yt-dlp inspection service.
    }

    [RelayCommand]
    private void StartDownload()
    {
    }

    [RelayCommand]
    private void AddToQueue()
    {
    }

    [RelayCommand]
    private void Inspect() => HasInspectedMedia = true;

    [RelayCommand]
    private void ClearSource()
    {
        SourceUrl = string.Empty;
        HasInspectedMedia = false;
    }

    private void LoadSampleInspection()
    {
        Streams.Add(new StreamOption { Resolution = "2160p 4K", Format = "MP4", VideoCodec = "H.264", AudioCodec = "AAC 320k", Fps = 30, Size = "1.72 GB", IsRecommended = true });
        Streams.Add(new StreamOption { Resolution = "1440p 2K", Format = "MP4", VideoCodec = "H.264", AudioCodec = "AAC 192k", Fps = 30, Size = "853 MB" });
        Streams.Add(new StreamOption { Resolution = "1080p HD", Format = "MP4", VideoCodec = "H.264", AudioCodec = "AAC 192k", Fps = 30, Size = "476 MB" });
        Streams.Add(new StreamOption { Resolution = "720p HD", Format = "MP4", VideoCodec = "H.264", AudioCodec = "AAC 128k", Fps = 30, Size = "281 MB" });
        Streams.Add(new StreamOption { Resolution = "480p", Format = "MP4", VideoCodec = "H.264", AudioCodec = "AAC 128k", Fps = 30, Size = "153 MB" });
        Streams.Add(new StreamOption { Resolution = "360p", Format = "MP4", VideoCodec = "H.264", AudioCodec = "AAC 96k", Fps = 30, Size = "98 MB" });
        Streams.Add(new StreamOption { Resolution = "240p", Format = "MP4", VideoCodec = "H.264", AudioCodec = "AAC 64k", Fps = 30, Size = "58 MB" });
        Streams.Add(new StreamOption { Resolution = "144p", Format = "MP4", VideoCodec = "H.264", AudioCodec = "AAC 64k", Fps = 30, Size = "34 MB" });
        SelectedStream = Streams[0];
        OnPropertyChanged(nameof(StreamCountText));
    }
}
