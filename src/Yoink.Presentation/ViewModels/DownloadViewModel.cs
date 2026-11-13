using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yoink.Application.Abstractions;
using Yoink.Application.Downloads;
using Yoink.Domain.Media;
using Yoink.Domain.Settings;
using Yoink.Presentation.Navigation;
using Yoink.Presentation.Services;
using Yoink.Presentation.ViewModels.Models;
using Yoink.Shared;
using Yoink.Shared.Validation;

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
    [ObservableProperty] private bool _isRefreshingStreams;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // URL heuristics are not reliable enough to present as a detected type (a YouTube video URL
    // may include a radio/playlist query parameter). Only show a type after yt-dlp resolves it.
    public bool IsDetectedTypeValid => HasInspectedMedia && DetectedType != DetectedSourceType.Invalid;
    public string DetectedTypeDisplay => IsDetectedTypeValid ? DetectedType.ToString() : "—";

    partial void OnDetectedTypeChanged(DetectedSourceType value)
    {
        OnPropertyChanged(nameof(IsDetectedTypeValid));
        OnPropertyChanged(nameof(DetectedTypeDisplay));
    }

    partial void OnHasInspectedMediaChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDetectedTypeValid));
        OnPropertyChanged(nameof(DetectedTypeDisplay));
    }

    private Task? _currentInspectTask;
    private int _inspectionVersion;

    // ---- Media preview ----
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _thumbnailUrl = string.Empty;
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
    [ObservableProperty] private string _fetchingDetailsText = "Fetching media information and streams from URL…";

    // ---- Download mode ----
    [ObservableProperty] private DownloadMode _selectedMode = DownloadMode.Video;

    [RelayCommand]
    private void SelectMode(DownloadMode mode) => SelectedMode = mode;

    // ---- Video controls ----
    [ObservableProperty] private string _quality = "Best Quality";
    [ObservableProperty] private string _container = "MP4";
    [ObservableProperty] private string _videoCodec = "Any";
    [ObservableProperty] private string _audioQuality = "Best";
    [ObservableProperty] private string _audioContainer = "MP3";
    [ObservableProperty] private string _subtitleLanguage = "en";
    [ObservableProperty] private string _subtitleFormat = "SRT";
    [ObservableProperty] private string _thumbnailFormat = "Best / JPG";
    [ObservableProperty] private bool _embedSubtitles = true;
    [ObservableProperty] private bool _embedMetadata = true;
    [ObservableProperty] private bool _embedThumbnail = true;
    [ObservableProperty] private string _chapters = "Embed as chapters";
    [ObservableProperty] private string _outputFolder = string.Empty;
    [ObservableProperty] private string _fileNameTemplate = "%(title)s [%(id)s].%(ext)s";

    public ObservableCollection<string> QualityOptions { get; } =
        new() { "Best Quality", "2160p", "1440p", "1080p", "720p", "480p", "360p", "240p" };
    public ObservableCollection<string> ContainerOptions { get; } = new() { "Any", "MP4", "MKV", "WEBM" };
    public ObservableCollection<string> VideoCodecOptions { get; } =
        new() { "Any", "H.264 (AVC)", "H.265 (HEVC)", "VP9", "AV1" };
    public ObservableCollection<string> AudioQualityOptions { get; } =
        new() { "Best", "320 kbps", "256 kbps", "192 kbps", "128 kbps", "96 kbps" };
    public ObservableCollection<string> AudioContainerOptions { get; } =
        new() { "Any", "MP3", "M4A", "OPUS", "FLAC", "WAV", "AAC" };
    public ObservableCollection<SubtitleLanguageOption> SubtitleOptions { get; } =
        new() { new("None", "None") };
    public ObservableCollection<string> SubtitleFormatOptions { get; } =
        new() { "SRT", "VTT", "ASS" };
    public ObservableCollection<string> ThumbnailFormatOptions { get; } =
        new() { "Best / JPG", "PNG", "WEBP" };
    public ObservableCollection<string> ChapterOptions { get; } =
        new() { "Ignore", "Embed as chapters", "Split by chapter" };

    // ---- Available streams ----
    public ObservableCollection<StreamOption> VideoStreams { get; } = new();
    public ObservableCollection<StreamOption> AudioStreams { get; } = new();
    public ObservableCollection<StreamOption> SubtitleStreams { get; } = new();

    [ObservableProperty] private StreamOption? _selectedVideoStream;
    [ObservableProperty] private StreamOption? _selectedAudioStream;
    [ObservableProperty] private StreamOption? _selectedSubtitleStream;

    public string StreamCountText => SelectedMode switch
    {
        DownloadMode.Audio => $"{AudioStreams.Count} audio streams available",
        DownloadMode.Subtitles => $"{SubtitleStreams.Count} subtitle tracks available",
        _ => $"{VideoStreams.Count} video streams available"
    };

    public string SelectedStreamText => CurrentSelectedStream() is { } stream
        ? $"Selected: {stream.Resolution}"
        : "No stream selected";

    partial void OnSelectedVideoStreamChanged(StreamOption? value) => OnPropertyChanged(nameof(SelectedStreamText));
    partial void OnSelectedAudioStreamChanged(StreamOption? value) => OnPropertyChanged(nameof(SelectedStreamText));
    partial void OnSelectedSubtitleStreamChanged(StreamOption? value)
    {
        if (!string.IsNullOrWhiteSpace(value?.LanguageCode))
        {
            SubtitleLanguage = value.LanguageCode;
        }
        OnPropertyChanged(nameof(SelectedStreamText));
    }

    partial void OnSubtitleLanguageChanged(string value)
    {
        var matchingStream = SubtitleStreams.FirstOrDefault(stream =>
            string.Equals(stream.LanguageCode, value, StringComparison.OrdinalIgnoreCase));
        if (!ReferenceEquals(SelectedSubtitleStream, matchingStream))
        {
            SelectedSubtitleStream = matchingStream;
        }
    }

    partial void OnSelectedModeChanged(DownloadMode value)
    {
        OnPropertyChanged(nameof(StreamCountText));
        OnPropertyChanged(nameof(SelectedStreamText));
    }

    // Choosing a quality preset clears any explicit stream pick so the height-based selector applies.
    partial void OnQualityChanged(string value)
    {
        if (SelectedVideoStream is not null && SelectedVideoStream.Resolution != value)
        {
            SelectedVideoStream = null;
        }
    }

    partial void OnSourceUrlChanged(string value)
    {
        var sanitized = TextSanitizer.TruncateToSingleLine(value);
        if (sanitized != value)
        {
            SourceUrl = sanitized;
            return;
        }

        // Do not show a speculative URL-based type while inspection is in flight.
        _inspectionVersion++;
        IsInspecting = false;
        HasInspectedMedia = false;
        DetectedType = DetectedSourceType.Invalid;
        ThumbnailUrl = string.Empty;
        ClearStreams();
        _metadata = null;
        OnPropertyChanged(nameof(StreamCountText));
        if (!string.IsNullOrWhiteSpace(value) && Uri.TryCreate(value.Trim(), UriKind.Absolute, out _))
        {
            _currentInspectTask = Inspect();
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

        var sourceUrl = SourceUrl.Trim();
        var inspectionVersion = _inspectionVersion;
        IsInspecting = true;
        StatusMessage = "Fetching media details…";
        try
        {
            var result = await _inspector.InspectAsync(sourceUrl);

            // A prior inspection may finish after the user pastes another URL. Its metadata must
            // not repopulate the page or overwrite the newer inspection's state.
            if (inspectionVersion != _inspectionVersion)
            {
                return;
            }

            if (result.IsFailure)
            {
                var err = result.Error ?? "Inspection failed.";
                if (err.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                    || err.Contains("geo", StringComparison.OrdinalIgnoreCase)
                    || err.Contains("blocked", StringComparison.OrdinalIgnoreCase)
                    || err.Contains("login", StringComparison.OrdinalIgnoreCase)
                    || err.Contains("403", StringComparison.OrdinalIgnoreCase))
                {
                    StatusMessage = $"{err} — Tip: If this site is age-restricted or blocked by your network/ISP, configure a Proxy or Cookies file in Settings → Network.";
                }
                else
                {
                    StatusMessage = err;
                }

                HasInspectedMedia = false;
                ThumbnailUrl = string.Empty;
                return;
            }

            ApplyMetadata(result.Value!);
            HasInspectedMedia = true;
            StatusMessage = string.Empty;
        }
        finally
        {
            if (inspectionVersion == _inspectionVersion)
            {
                IsInspecting = false;
            }
        }
    }

    [RelayCommand]
    private async Task RefreshStreams()
    {
        if (string.IsNullOrWhiteSpace(SourceUrl) || IsRefreshingStreams)
        {
            return;
        }

        var sourceUrl = SourceUrl.Trim();
        IsRefreshingStreams = true;
        try
        {
            var result = await _inspector.InspectAsync(sourceUrl);
            if (result.IsFailure)
            {
                StatusMessage = result.Error ?? "Could not refresh streams.";
                return;
            }

            // Refresh the format/track catalog without replacing any displayed media-card data.
            if (!string.Equals(sourceUrl, SourceUrl.Trim(), StringComparison.Ordinal))
            {
                return;
            }

            _metadata = result.Value!;
            RebuildStreams(result.Value!);
            StatusMessage = "Streams refreshed.";
        }
        finally
        {
            IsRefreshingStreams = false;
        }
    }

    [RelayCommand]
    private async Task StartDownload()
    {
        if (string.IsNullOrWhiteSpace(SourceUrl))
        {
            StatusMessage = "Paste a URL first.";
            return;
        }

        if (_currentInspectTask is { IsCompleted: false })
        {
            await _currentInspectTask;
        }
        else if (!HasInspectedMedia && !IsInspecting)
        {
            _currentInspectTask = Inspect();
            await _currentInspectTask;
        }

        if (!TryBuildRequest(out var request, out var info))
        {
            return;
        }

        _coordinator.Enqueue(request!, info!, startImmediately: true);
        _navigation.Navigate(AppPage.Queue);
    }

    [RelayCommand]
    private async Task AddToQueue()
    {
        if (string.IsNullOrWhiteSpace(SourceUrl))
        {
            StatusMessage = "Paste a URL first.";
            return;
        }

        if (_currentInspectTask is { IsCompleted: false })
        {
            await _currentInspectTask;
        }
        else if (!HasInspectedMedia && !IsInspecting)
        {
            _currentInspectTask = Inspect();
            await _currentInspectTask;
        }

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
            SourceUrl = TextSanitizer.TruncateToSingleLine(text);
        }
    }

    [RelayCommand]
    private void ClearSource()
    {
        SourceUrl = string.Empty;
        ThumbnailUrl = string.Empty;
        HasInspectedMedia = false;
        ClearStreams();
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
        var videoOutputContainer = ResolveVideoOutputContainer(settings.PreferredVideoContainer);
        var audioOutputContainer = ResolveAudioOutputContainer(settings.PreferredAudioContainer);

        request = new DownloadRequest
        {
            Url = SourceUrl.Trim(),
            Kind = kind,
            // Explicit format/height selection is only meaningful for a video download.
            FormatId = kind switch
            {
                DownloadKind.Video => SelectedVideoStream?.FormatId,
                DownloadKind.Audio => SelectedAudioStream?.FormatId,
                _ => null
            },
            MaxHeight = kind == DownloadKind.Video && SelectedVideoStream?.Height > 0 ? SelectedVideoStream.Height : kind == DownloadKind.Video ? ParseHeight(Quality) : 0,
            VideoContainer = videoOutputContainer,
            AudioContainer = audioOutputContainer,
            VideoCodecPreference = VideoCodec is "Any" or "Auto" ? null : VideoCodec,
            AudioQuality = MapAudioQuality(AudioQuality),
            OutputFolder = string.IsNullOrWhiteSpace(OutputFolder) ? settings.DefaultDownloadFolder : OutputFolder,
            FilenameTemplate = string.IsNullOrWhiteSpace(FileNameTemplate) ? settings.FilenameTemplate : FileNameTemplate,
            EmbedSubtitles = false,
            EmbedMetadata = EmbedMetadata,
            EmbedThumbnail = EmbedThumbnail,
            SplitChapters = Chapters == "Split by chapter",
            SubtitleLanguages = MapSubtitleLang(SubtitleLanguage),
            SubtitleFormat = SubtitleFormat.ToLowerInvariant(),
            CookiesFile = settings.CookiesFile,
            Proxy = settings.Proxy,
            SpeedLimitKbps = settings.DownloadSpeedLimitKbps,
            KnownTitle = title
        };

        var qualityLabel = kind switch
        {
            DownloadKind.Video => SelectedVideoStream?.Resolution ?? Quality,
            DownloadKind.Audio => SelectedAudioStream?.Resolution ?? AudioQuality,
            DownloadKind.Subtitles => SelectedSubtitleStream?.Resolution ?? LanguageDisplayName(SubtitleLanguage),
            _ => ThumbnailFormat
        };
        var formatLabel = kind switch
        {
            DownloadKind.Audio => audioOutputContainer.ToUpperInvariant(),
            DownloadKind.Subtitles => SubtitleFormat.ToUpperInvariant(),
            DownloadKind.Thumbnail => "JPG",
            _ => videoOutputContainer.ToUpperInvariant()
        };

        info = new EnqueueInfo(title, channel, qualityLabel, formatLabel, _metadata?.ThumbnailUrl, null);
        return true;
    }

    private void ApplyMetadata(MediaMetadata m)
    {
        _metadata = m;
        Title = m.Title;
        ThumbnailUrl = m.ThumbnailUrl ?? string.Empty;
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

        var subCount = m.SubtitleLanguages.Count(IsUsableSubtitleLanguage);
        SubtitleAvailabilityText = subCount > 0 ? $"Subtitles available ({subCount})" : "No subtitles";

        RebuildSubtitleOptions(m);
        RebuildQualityOptions(m);
        RebuildStreams(m);
    }

    private static bool IsWatermarked(MediaFormat f) =>
        f.FormatNote?.Contains("watermark", StringComparison.OrdinalIgnoreCase) == true
        || f.FormatId.Contains("download", StringComparison.OrdinalIgnoreCase)
        || f.FormatId.Contains("wmk", StringComparison.OrdinalIgnoreCase);

    private void RebuildFilteredStreams()
    {
        if (_metadata is not null)
        {
            RebuildStreams(_metadata);
        }
    }

    private void RebuildStreams(MediaMetadata m)
    {
        ClearStreams();
        var videoFormats = m.Formats
            .Where(f => f.HasVideo && f.Height is > 0)
            .Where(IsCompatibleVideoContainer)
            .Where(IsCompatibleVideoCodec)
            .OrderBy(f => IsWatermarked(f) ? 1 : 0) // Clean/unwatermarked streams first
            .ThenByDescending(f => f.Height)
            .ThenByDescending(f => f.TotalBitrateKbps ?? 0)
            .ToList();

        var best = videoFormats.FirstOrDefault(f => !IsWatermarked(f)) ?? videoFormats.FirstOrDefault();
        foreach (var f in videoFormats)
        {
            var resLabel = FormatResolution(f);
            if (IsWatermarked(f))
            {
                resLabel += " (Watermarked)";
            }

            VideoStreams.Add(new StreamOption
            {
                FormatId = f.FormatId,
                Height = f.Height ?? 0,
                Resolution = resLabel,
                Format = Container.Equals("Any", StringComparison.OrdinalIgnoreCase)
                    ? (f.Extension ?? "video").ToUpperInvariant()
                    : Container.ToUpperInvariant(),
                VideoCodec = ShortCodec(f.VideoCodec),
                AudioCodec = f.HasAudio ? ShortCodec(f.AudioCodec) : "AUTO",
                Fps = (int)Math.Round(f.Fps ?? 0),
                Hdr = f.IsHdr,
                Size = Formatting.Bytes(f.EffectiveSizeBytes),
                IsRecommended = ReferenceEquals(f, best)
            });
        }

        var audioFormats = m.Formats
            .Where(f => IsKnownAudio(f) && !f.HasVideo)
            .Where(IsCompatibleAudioContainer)
            .OrderByDescending(f => f.AudioBitrateKbps ?? f.TotalBitrateKbps ?? 0)
            .ThenByDescending(f => f.EffectiveSizeBytes ?? 0)
            .ToList();

        // Some extractors expose only combined A/V formats. They are still valid audio sources
        // because yt-dlp/ffmpeg can extract the selected audio track from them.
        if (audioFormats.Count == 0)
        {
            audioFormats = m.Formats
                .Where(IsKnownAudio)
                .Where(IsCompatibleAudioContainer)
                .OrderByDescending(f => f.AudioBitrateKbps ?? f.TotalBitrateKbps ?? 0)
                .ThenByDescending(f => f.EffectiveSizeBytes ?? 0)
                .ToList();
        }

        foreach (var f in audioFormats)
        {
            var bitrate = f.AudioBitrateKbps;
            AudioStreams.Add(new StreamOption
            {
                FormatId = f.FormatId,
                Resolution = bitrate is > 0 ? $"{bitrate:0} kbps" : FriendlyAudioQuality(f.FormatNote),
                Format = ResolveAudioOutputLabel(f),
                VideoCodec = "—",
                AudioCodec = ShortCodec(f.AudioCodec),
                Size = Formatting.Bytes(f.EffectiveSizeBytes),
                IsRecommended = ReferenceEquals(f, audioFormats.FirstOrDefault())
            });
        }

        foreach (var code in m.SubtitleLanguages
                     .Where(IsUsableSubtitleLanguage)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(LanguageDisplayName))
        {
            SubtitleStreams.Add(CreateSubtitleStream(code, false));
        }
        OnPropertyChanged(nameof(StreamCountText));
        OnPropertyChanged(nameof(SelectedStreamText));
    }

    private void RebuildQualityOptions(MediaMetadata m)
    {
        var heights = m.Formats.Where(f => f.HasVideo && f.Height is > 0)
            .OrderBy(f => IsWatermarked(f) ? 1 : 0)
            .Select(f => f.Height!.Value).Distinct().OrderByDescending(h => h).ToList();
        QualityOptions.Clear();
        QualityOptions.Add("Best Quality");
        foreach (var h in heights)
        {
            QualityOptions.Add($"{h}p");
        }

        var key = SourceVideoQuality.SourceKey(SourceUrl);
        var saved = _settings.Current.SourceVideoQualities;
        var closest = key is not null && saved is not null && saved.TryGetValue(key, out var preferred)
            ? SourceVideoQuality.ClosestHeight(heights, preferred)
            : null;
        Quality = closest is { } height ? $"{height}p" : "Best Quality";
    }

    private void RebuildSubtitleOptions(MediaMetadata m)
    {
        SubtitleOptions.Clear();
        SubtitleOptions.Add(new SubtitleLanguageOption("None", "None"));
        foreach (var option in m.SubtitleLanguages
                     .Where(IsUsableSubtitleLanguage)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .Select(lang => new SubtitleLanguageOption(lang, LanguageDisplayName(lang)))
                     .OrderBy(option => option.DisplayName)
                     .Take(60))
        {
            SubtitleOptions.Add(option);
        }

        SubtitleLanguage = SubtitleOptions.Any(x => x.Code == "en") ? "en" : SubtitleOptions.FirstOrDefault()?.Code ?? "None";
    }

    partial void OnContainerChanged(string value) => RebuildFilteredStreams();

    partial void OnVideoCodecChanged(string value) => RebuildFilteredStreams();

    partial void OnAudioContainerChanged(string value) => RebuildFilteredStreams();

    private StreamOption? CurrentSelectedStream() => SelectedMode switch
    {
        DownloadMode.Video => SelectedVideoStream,
        DownloadMode.Audio => SelectedAudioStream,
        DownloadMode.Subtitles => SelectedSubtitleStream,
        _ => null
    };

    private void ClearStreams()
    {
        VideoStreams.Clear();
        AudioStreams.Clear();
        SubtitleStreams.Clear();
        SelectedVideoStream = null;
        SelectedAudioStream = null;
        SelectedSubtitleStream = null;
        OnPropertyChanged(nameof(StreamCountText));
        OnPropertyChanged(nameof(SelectedStreamText));
    }

    private bool IsCompatibleVideoContainer(MediaFormat format)
    {
        var extension = format.Extension?.TrimStart('.').ToLowerInvariant();
        return Container.ToUpperInvariant() switch
        {
            "ANY" => true,
            "MP4" => extension == "mp4",
            "WEBM" => extension == "webm",
            // MKV is a merge/output container and accepts all source video codecs.
            "MKV" => true,
            _ => true
        };
    }

    private bool IsCompatibleVideoCodec(MediaFormat format)
    {
        if (VideoCodec is "Any" or "Auto")
        {
            return true;
        }

        var selectedCodec = VideoCodec switch
        {
            "H.264 (AVC)" => "H.264",
            "H.265 (HEVC)" => "H.265",
            _ => VideoCodec
        };
        return string.Equals(ShortCodec(format.VideoCodec), selectedCodec, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsCompatibleAudioContainer(MediaFormat format)
    {
        var extension = format.Extension?.TrimStart('.').ToLowerInvariant();
        var codec = ShortCodec(format.AudioCodec);
        return AudioContainer.ToUpperInvariant() switch
        {
            "ANY" => true,
            "M4A" or "AAC" => extension is "m4a" or "mp4" || codec == "AAC",
            "OPUS" => extension is "webm" or "opus" || codec == "Opus",
            // These are conversion targets; any known source audio stream can produce them.
            "MP3" or "FLAC" or "WAV" => true,
            _ => true
        };
    }

    private string ResolveVideoOutputContainer(string fallback)
    {
        if (!Container.Equals("Any", StringComparison.OrdinalIgnoreCase))
        {
            return Container.ToLowerInvariant();
        }

        var selected = SelectedVideoStream?.Format?.ToLowerInvariant();
        return selected is "mp4" or "webm" or "mkv" ? selected : fallback.ToLowerInvariant();
    }

    private string ResolveAudioOutputContainer(string fallback)
    {
        if (!AudioContainer.Equals("Any", StringComparison.OrdinalIgnoreCase))
        {
            return AudioContainer.ToLowerInvariant();
        }

        if (SelectedAudioStream is { } selected)
        {
            if (selected.AudioCodec.Equals("Opus", StringComparison.OrdinalIgnoreCase))
            {
                return "opus";
            }
            if (selected.AudioCodec.Equals("AAC", StringComparison.OrdinalIgnoreCase))
            {
                return "m4a";
            }
        }

        return fallback.ToLowerInvariant();
    }

    private string ResolveAudioOutputLabel(MediaFormat format)
    {
        if (!AudioContainer.Equals("Any", StringComparison.OrdinalIgnoreCase))
        {
            return AudioContainer.ToUpperInvariant();
        }

        return ShortCodec(format.AudioCodec) switch
        {
            "Opus" => "OPUS",
            "AAC" => "M4A",
            _ => (format.Extension ?? "AUDIO").ToUpperInvariant()
        };
    }

    private static bool IsKnownAudio(MediaFormat format) =>
        format.HasAudio
        && !string.Equals(format.AudioCodec, "unknown", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(format.AudioCodec, "none", StringComparison.OrdinalIgnoreCase);

    private static bool IsUsableSubtitleLanguage(string code) =>
        !string.IsNullOrWhiteSpace(code)
        && !code.Equals("live_chat", StringComparison.OrdinalIgnoreCase);

    private static string FriendlyAudioQuality(string? formatNote)
    {
        if (string.IsNullOrWhiteSpace(formatNote))
        {
            return "Audio track";
        }

        var note = formatNote.Split(',')[0].Trim();
        return note.Length == 0 ? "Audio track" : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(note);
    }

    private static StreamOption CreateSubtitleStream(string code, bool automatic) => new()
    {
        LanguageCode = code,
        Resolution = LanguageDisplayName(code),
        Format = automatic ? "AUTO" : "MANUAL",
        VideoCodec = "—",
        AudioCodec = "—",
        Size = "—",
        IsAutomaticSubtitle = automatic
    };

    private static string LanguageDisplayName(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return "None";
        }

        var isOriginal = code.EndsWith("-orig", StringComparison.OrdinalIgnoreCase);
        var normalized = code.Replace("-orig", string.Empty, StringComparison.OrdinalIgnoreCase);
        try
        {
            var culture = CultureInfo.GetCultureInfo(normalized);
            return isOriginal ? $"{culture.EnglishName} (Original)" : culture.EnglishName;
        }
        catch (CultureNotFoundException)
        {
            var baseCode = normalized.Split('-', '_')[0];
            try
            {
                var name = CultureInfo.GetCultureInfo(baseCode).EnglishName;
                return isOriginal ? $"{name} (Original)" : name;
            }
            catch (CultureNotFoundException)
            {
                return code;
            }
        }
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
