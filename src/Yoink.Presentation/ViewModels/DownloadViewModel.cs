using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yoink.Application.Abstractions;
using Yoink.Application.Downloads;
using Yoink.Domain.Media;
using Yoink.Presentation.Navigation;
using Yoink.Presentation.Services;
using Yoink.Presentation.ViewModels.Models;
using Yoink.Shared;

namespace Yoink.Presentation.ViewModels;

public enum DownloadMode
{
    Video,
    Audio,
    Subtitles,
    Thumbnail
}

/// <summary>Backs the Download page: paste, inspect, configure, and start/queue a download.</summary>
public partial class DownloadViewModel : ObservableObject
{
    private readonly IMediaInspector _inspector;
    private readonly DownloadCoordinator _coordinator;
    private readonly ISettingsService _settings;
    private readonly ISystemAccess _system;
    private readonly INavigationService _navigation;

    private MediaMetadata? _metadata;

    public DownloadViewModel(
        IMediaInspector inspector,
        DownloadCoordinator coordinator,
        ISettingsService settings,
        ISystemAccess system,
        INavigationService navigation)
    {
        _inspector = inspector;
        _coordinator = coordinator;
        _settings = settings;
        _system = system;
        _navigation = navigation;

        OutputFolder = settings.Current.DefaultDownloadFolder;
        FileNameTemplate = settings.Current.FilenameTemplate;
        Container = settings.Current.PreferredVideoContainer.ToUpperInvariant();
    }

    // ---- Source bar ----
    [ObservableProperty] private string _sourceUrl = string.Empty;
    [ObservableProperty] private DetectedSourceType _detectedType = DetectedSourceType.Invalid;
    [ObservableProperty] private bool _hasInspectedMedia;
    [ObservableProperty] private bool _isInspecting;
    [ObservableProperty] private string _statusMessage = string.Empty;

    partial void OnSourceUrlChanged(string value) => DetectedType = MapKind(_inspector.DetectKind(value));

    // ---- Media preview ----
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _channelName = string.Empty;
    [ObservableProperty] private bool _channelVerified;
    [ObservableProperty] private string _durationText = "—";
    [ObservableProperty] private string _viewCountText = "—";
    [ObservableProperty] private string _uploadedAgoText = "—";
    [ObservableProperty] private string _uploadDateText = "—";
    [ObservableProperty] private string _viewCountFullText = "—";
    [ObservableProperty] private string _likeCountText = "—";
    [ObservableProperty] private string _descriptionPreview = string.Empty;
    [ObservableProperty] private string _subtitleAvailabilityText = "—";

    // ---- Download mode ----
    [ObservableProperty] private DownloadMode _selectedMode = DownloadMode.Video;

    [RelayCommand]
    private void SelectMode(DownloadMode mode) => SelectedMode = mode;

    // ---- Video controls ----
    [ObservableProperty] private string _quality = "Best Quality";
    [ObservableProperty] private string _container = "MP4";
    [ObservableProperty] private string _videoCodec = "Auto";
    [ObservableProperty] private string _audioQuality = "Best";
    [ObservableProperty] private string _subtitleLanguage = "English";
    [ObservableProperty] private bool _embedSubtitles = true;
    [ObservableProperty] private bool _embedMetadata = true;
    [ObservableProperty] private bool _embedThumbnail = true;
    [ObservableProperty] private string _chapters = "Embed as chapters";
    [ObservableProperty] private string _outputFolder = string.Empty;
    [ObservableProperty] private string _fileNameTemplate = "%(title)s [%(id)s].%(ext)s";

    public ObservableCollection<string> QualityOptions { get; } =
        new() { "Best Quality", "2160p", "1440p", "1080p", "720p", "480p", "360p", "240p" };
    public ObservableCollection<string> ContainerOptions { get; } = new() { "MP4", "MKV", "WEBM" };
    public ObservableCollection<string> VideoCodecOptions { get; } =
        new() { "Auto", "H.264 (AVC)", "H.265 (HEVC)", "VP9", "AV1" };
    public ObservableCollection<string> AudioQualityOptions { get; } =
        new() { "Best", "320 kbps", "256 kbps", "192 kbps", "128 kbps", "96 kbps" };
    public ObservableCollection<string> SubtitleOptions { get; } = new() { "None", "English" };
    public ObservableCollection<string> ChapterOptions { get; } =
        new() { "Ignore", "Embed as chapters", "Split by chapter" };

    // ---- Available streams ----
    public ObservableCollection<StreamOption> Streams { get; } = new();

    [ObservableProperty] private StreamOption? _selectedStream;

    public string StreamCountText => $"{Streams.Count} streams available";
    public string SelectedStreamText => SelectedStream is null ? "No stream selected" : $"Selected: {SelectedStream.Resolution}";

    partial void OnSelectedStreamChanged(StreamOption? value) => OnPropertyChanged(nameof(SelectedStreamText));

    // Choosing a quality preset clears any explicit stream pick so the height-based selector applies.
    partial void OnQualityChanged(string value)
    {
        if (SelectedStream is not null && SelectedStream.Resolution != value)
        {
            SelectedStream = null;
        }
    }

    [RelayCommand]
    private async Task Inspect()
    {
        if (string.IsNullOrWhiteSpace(SourceUrl))
        {
            StatusMessage = "Paste a URL first.";
            return;
        }

        IsInspecting = true;
        StatusMessage = "Inspecting…";
        try
        {
            var result = await _inspector.InspectAsync(SourceUrl);
            if (result.IsFailure)
            {
                StatusMessage = result.Error ?? "Inspection failed.";
                HasInspectedMedia = false;
                return;
            }

            ApplyMetadata(result.Value!);
            HasInspectedMedia = true;
            StatusMessage = string.Empty;
        }
        finally
        {
            IsInspecting = false;
        }
    }

    [RelayCommand]
    private async Task RefreshStreams()
    {
        if (!string.IsNullOrWhiteSpace(SourceUrl))
        {
            await Inspect();
        }
    }

    [RelayCommand]
    private void StartDownload()
    {
        if (!TryBuildRequest(out var request, out var info))
        {
            return;
        }

        _coordinator.Enqueue(request!, info!, startImmediately: true);
        _navigation.Navigate(AppPage.Queue);
    }

    [RelayCommand]
    private void AddToQueue()
    {
        if (!TryBuildRequest(out var request, out var info))
        {
            return;
        }

        _coordinator.Enqueue(request!, info!, startImmediately: false);
        StatusMessage = "Added to queue.";
    }

    [RelayCommand]
    private void Paste()
    {
        var text = _system.ReadClipboardText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            SourceUrl = text.Trim();
        }
    }

    [RelayCommand]
    private void ClearSource()
    {
        SourceUrl = string.Empty;
        HasInspectedMedia = false;
        Streams.Clear();
        _metadata = null;
        StatusMessage = string.Empty;
        OnPropertyChanged(nameof(StreamCountText));
    }

    [RelayCommand]
    private void BrowseOutputFolder()
    {
        var picked = _system.PickFolder(OutputFolder);
        if (picked is not null)
        {
            OutputFolder = picked;
        }
    }

    [RelayCommand]
    private void InsertTemplateVariable(string token) => FileNameTemplate += token;

    private bool TryBuildRequest(out DownloadRequest? request, out EnqueueInfo? info)
    {
        request = null;
        info = null;
        if (string.IsNullOrWhiteSpace(SourceUrl))
        {
            StatusMessage = "Paste a URL first.";
            return false;
        }

        var kind = SelectedMode switch
        {
            DownloadMode.Audio => DownloadKind.Audio,
            DownloadMode.Subtitles => DownloadKind.Subtitles,
            DownloadMode.Thumbnail => DownloadKind.Thumbnail,
            _ => DownloadKind.Video
        };

        var settings = _settings.Current;
        var title = _metadata?.Title ?? SourceUrl;
        var channel = _metadata?.Channel ?? string.Empty;

        request = new DownloadRequest
        {
            Url = SourceUrl.Trim(),
            Kind = kind,
            FormatId = SelectedStream?.FormatId,
            MaxHeight = SelectedStream?.Height > 0 ? SelectedStream.Height : ParseHeight(Quality),
            VideoContainer = Container.ToLowerInvariant(),
            AudioContainer = settings.PreferredAudioContainer,
            VideoCodecPreference = VideoCodec == "Auto" ? null : VideoCodec,
            AudioQuality = MapAudioQuality(AudioQuality),
            OutputFolder = string.IsNullOrWhiteSpace(OutputFolder) ? settings.DefaultDownloadFolder : OutputFolder,
            FilenameTemplate = string.IsNullOrWhiteSpace(FileNameTemplate) ? settings.FilenameTemplate : FileNameTemplate,
            EmbedSubtitles = EmbedSubtitles && SubtitleLanguage != "None",
            EmbedMetadata = EmbedMetadata,
            EmbedThumbnail = EmbedThumbnail,
            SplitChapters = Chapters == "Split by chapter",
            SubtitleLanguages = MapSubtitleLang(SubtitleLanguage),
            CookiesFile = settings.CookiesFile,
            Proxy = settings.Proxy,
            SpeedLimitKbps = settings.DownloadSpeedLimitKbps,
            KnownTitle = title
        };

        var qualityLabel = SelectedStream?.Resolution ?? Quality;
        var formatLabel = kind switch
        {
            DownloadKind.Audio => settings.PreferredAudioContainer.ToUpperInvariant(),
            DownloadKind.Subtitles => "SRT",
            DownloadKind.Thumbnail => "JPG",
            _ => Container
        };

        info = new EnqueueInfo(title, channel, qualityLabel, formatLabel, _metadata?.ThumbnailUrl,
            SelectedStream is not null ? null : null);
        return true;
    }

    private void ApplyMetadata(MediaMetadata m)
    {
        _metadata = m;
        Title = m.Title;
        ChannelName = m.Channel ?? m.Uploader ?? string.Empty;
        ChannelVerified = m.ChannelFollowerCount is > 100_000;
        DurationText = Formatting.Duration(m.DurationSeconds);
        ViewCountText = m.ViewCount is { } vc ? $"{Formatting.Count(vc)} views" : "—";
        ViewCountFullText = Formatting.FullCount(m.ViewCount);
        LikeCountText = Formatting.Count(m.LikeCount);
        UploadedAgoText = Formatting.RelativeDate(m.UploadDate);
        UploadDateText = Formatting.ShortDate(m.UploadDate);
        DescriptionPreview = Truncate(m.Description, 220);
        DetectedType = MapKind(m.SourceKind);

        var subCount = m.SubtitleLanguages.Count + m.AutomaticCaptionLanguages.Count;
        SubtitleAvailabilityText = subCount > 0 ? $"Subtitles available ({subCount})" : "No subtitles";

        RebuildSubtitleOptions(m);
        RebuildQualityOptions(m);
        RebuildStreams(m);
    }

    private void RebuildStreams(MediaMetadata m)
    {
        Streams.Clear();
        var videoFormats = m.Formats
            .Where(f => f.HasVideo && f.Height is > 0)
            .OrderByDescending(f => f.Height)
            .ThenByDescending(f => f.TotalBitrateKbps ?? 0)
            .GroupBy(f => f.Height)
            .Select(g => g.First())
            .ToList();

        var best = videoFormats.FirstOrDefault();
        foreach (var f in videoFormats)
        {
            Streams.Add(new StreamOption
            {
                FormatId = f.FormatId,
                Height = f.Height ?? 0,
                Resolution = FormatResolution(f),
                Format = (f.Extension ?? "mp4").ToUpperInvariant(),
                VideoCodec = ShortCodec(f.VideoCodec),
                AudioCodec = f.HasAudio ? ShortCodec(f.AudioCodec) : "video only",
                Fps = (int)Math.Round(f.Fps ?? 0),
                Hdr = f.IsHdr,
                Size = Formatting.Bytes(f.EffectiveSizeBytes),
                IsRecommended = ReferenceEquals(f, best)
            });
        }

        SelectedStream = Streams.FirstOrDefault();
        OnPropertyChanged(nameof(StreamCountText));
    }

    private void RebuildQualityOptions(MediaMetadata m)
    {
        var heights = m.Formats.Where(f => f.HasVideo && f.Height is > 0)
            .Select(f => f.Height!.Value).Distinct().OrderByDescending(h => h).ToList();
        QualityOptions.Clear();
        QualityOptions.Add("Best Quality");
        foreach (var h in heights)
        {
            QualityOptions.Add($"{h}p");
        }

        Quality = "Best Quality";
    }

    private void RebuildSubtitleOptions(MediaMetadata m)
    {
        SubtitleOptions.Clear();
        SubtitleOptions.Add("None");
        foreach (var lang in m.SubtitleLanguages.Concat(m.AutomaticCaptionLanguages).Distinct().Take(30))
        {
            SubtitleOptions.Add(lang);
        }

        SubtitleLanguage = SubtitleOptions.Contains("en") ? "en" : SubtitleOptions.FirstOrDefault() ?? "None";
    }

    private static string FormatResolution(MediaFormat f)
    {
        var label = f.Height switch
        {
            >= 4320 => "4320p 8K",
            >= 2160 => "2160p 4K",
            >= 1440 => "1440p 2K",
            >= 1080 => "1080p HD",
            >= 720 => "720p HD",
            { } h => $"{h}p",
            _ => f.Resolution ?? "—"
        };
        return f.IsHdr ? $"{label} HDR" : label;
    }

    private static string ShortCodec(string? codec)
    {
        if (string.IsNullOrWhiteSpace(codec) || codec == "none")
        {
            return "—";
        }

        var c = codec.ToLowerInvariant();
        if (c.StartsWith("avc")) return "H.264";
        if (c.StartsWith("hev") || c.StartsWith("hvc")) return "H.265";
        if (c.StartsWith("vp9") || c.StartsWith("vp09")) return "VP9";
        if (c.StartsWith("av01")) return "AV1";
        if (c.StartsWith("mp4a")) return "AAC";
        if (c.StartsWith("opus")) return "Opus";
        return codec.Split('.')[0].ToUpperInvariant();
    }

    private static int ParseHeight(string quality)
    {
        var digits = new string(quality.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var h) ? h : 0;
    }

    private static string MapAudioQuality(string label) => label switch
    {
        "Best" => "0",
        _ => new string(label.TakeWhile(char.IsDigit).ToArray()) is { Length: > 0 } k ? $"{k}K" : "0"
    };

    private static string MapSubtitleLang(string label) => label switch
    {
        "None" => "en.*",
        "English" => "en.*",
        _ => $"{label}.*"
    };

    private static DetectedSourceType MapKind(MediaSourceKind kind) => kind switch
    {
        MediaSourceKind.Video => DetectedSourceType.Video,
        MediaSourceKind.Short => DetectedSourceType.Short,
        MediaSourceKind.Playlist => DetectedSourceType.Playlist,
        MediaSourceKind.Channel => DetectedSourceType.Channel,
        MediaSourceKind.Livestream => DetectedSourceType.Livestream,
        MediaSourceKind.AudioOnly => DetectedSourceType.AudioOnly,
        MediaSourceKind.Unsupported => DetectedSourceType.Unsupported,
        _ => DetectedSourceType.Invalid
    };

    private static string Truncate(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = text.ReplaceLineEndings(" ").Trim();
        return text.Length <= max ? text : text[..max].TrimEnd() + "…";
    }
}
