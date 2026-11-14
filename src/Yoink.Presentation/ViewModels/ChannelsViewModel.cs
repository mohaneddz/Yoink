using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yoink.Application.Abstractions;
using Yoink.Application.Downloads;
using Yoink.Domain.Entities;
using Yoink.Domain.Media;
using Yoink.Presentation.Extensions;
using Yoink.Presentation.Navigation;
using Yoink.Presentation.Services;
using Yoink.Presentation.ViewModels.Models;
using Yoink.Shared;
using Yoink.Shared.Validation;

namespace Yoink.Presentation.ViewModels;

/// <summary>Backs the Channels page: follow channels, review uploads, and queue their content.</summary>
public partial class ChannelsViewModel : ObservableObject
{
    private readonly ISavedChannelRepository _repository;
    private readonly IMediaInspector _inspector;
    private readonly DownloadCoordinator _coordinator;
    private readonly ISettingsService _settings;
    private readonly ISystemAccess _system;
    private readonly INavigationService _navigation;
    private readonly ILibraryRepository _library;
    private readonly Dictionary<(string Url, int Limit, string Proxy, string Cookies),
        (Result<PlaylistMetadata>[] Results, DateTimeOffset Expires)> _uploadCache = new();
    private int _uploadsLoadVersion;

    public ChannelsViewModel(
        ISavedChannelRepository repository,
        IMediaInspector inspector,
        DownloadCoordinator coordinator,
        ISettingsService settings,
        ISystemAccess system,
        INavigationService navigation,
        ILibraryRepository library)
    {
        _repository = repository;
        _inspector = inspector;
        _coordinator = coordinator;
        _settings = settings;
        _system = system;
        _navigation = navigation;
        _library = library;
        _uploadsView = CollectionViewSource.GetDefaultView(Uploads);
        _uploadsView.Filter = FilterUpload;
        _ = LoadAsync();
    }

    [ObservableProperty] private string _addChannelUrl = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    private readonly ICollectionView _uploadsView;
    private int _uploadLimit = 50;

    public ObservableCollection<SavedChannel> Channels { get; } = new();
    public ObservableCollection<ChannelUpload> Uploads { get; } = new();
    public ICollectionView UploadsView => _uploadsView;

    [ObservableProperty] private SavedChannel? _selectedChannel;
    [ObservableProperty] private string _selectedTab = "Recent Uploads";

    public int SelectedUploadCount => Uploads.Count(u => u.IsSelected);
    public bool HasSelectedUploads => Uploads.Any(u => u.IsSelected);

    /// <summary>Preset defaults used to seed the "Download selected" dialog.</summary>
    public string PresetFormatText => _settings.Current.PreferredVideoContainer.ToUpperInvariant();
    public string PresetFolderText => _settings.Current.DefaultDownloadFolder;

    private void RaiseSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedUploadCount));
        OnPropertyChanged(nameof(HasSelectedUploads));
    }

    [RelayCommand]
    private void SelectTab(string tab) => SelectedTab = tab;

    partial void OnSelectedTabChanged(string value) => _uploadsView.SafeRefresh();

    private bool FilterUpload(object obj)
    {
        if (obj is not ChannelUpload upload)
        {
            return false;
        }

        return SelectedTab?.ToLowerInvariant() switch
        {
            "videos" => upload.Type == ChannelUploadType.Video,
            "shorts" => upload.Type == ChannelUploadType.Short,
            "livestreams" => upload.Type == ChannelUploadType.Live,
            "playlists" => upload.Type == ChannelUploadType.Playlist,
            _ => true
        };
    }

    [RelayCommand]
    private async Task LoadMore()
    {
        if (IsBusy) return;
        if (SelectedChannel is not null)
        {
            _uploadLimit += 50;
            await LoadUploadsAsync(SelectedChannel);
        }
    }

    [RelayCommand]
    private async Task SelectChannel(SavedChannel channel)
    {
        SelectedChannel = channel;
        await LoadUploadsAsync(channel);
    }

    partial void OnSelectedChannelChanged(SavedChannel? value)
    {
        foreach (var c in Channels)
        {
            c.IsSelected = c == value;
        }
    }

    public async Task LoadAsync()
    {
        var entries = await _repository.GetAllAsync();
        Channels.Clear();
        foreach (var e in entries)
        {
            Channels.Add(Map(e));
        }

        if (Channels.Count > 0)
        {
            await SelectChannel(Channels[0]);
        }
    }

    partial void OnAddChannelUrlChanged(string value)
    {
        var sanitized = TextSanitizer.TruncateToSingleLine(value);
        if (sanitized != value)
        {
            AddChannelUrl = sanitized;
        }
    }

    [RelayCommand]
    private async Task AddChannel()
    {
        if (IsBusy) return;
        var raw = TextSanitizer.TruncateToSingleLine(AddChannelUrl);
        if (string.IsNullOrWhiteSpace(raw))
        {
            StatusMessage = "Paste a channel URL first.";
            return;
        }

        var url = TextSanitizer.NormalizeChannelInput(raw);
        var baseCandidate = StripTabSuffix(url);

        if (Channels.Any(c => string.Equals(c.ChannelUrl, url, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(c.ChannelUrl, baseCandidate, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "Channel already saved.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Fetching channel…";
        try
        {
            // Try videos tab first for yt-dlp flat-playlist metadata, fallback to base and raw url
            var result = await _inspector.InspectPlaylistAsync($"{baseCandidate}/videos", limit: 30);
            if (result.IsFailure)
            {
                result = await _inspector.InspectPlaylistAsync(baseCandidate, limit: 30);
            }
            if (result.IsFailure)
            {
                result = await _inspector.InspectPlaylistAsync(url, limit: 30);
            }

            if (result.IsFailure)
            {
                StatusMessage = result.Error ?? "Could not fetch channel.";
                return;
            }

            var meta = result.Value!;
            var resolvedChannelUrl = NormalizeChannelUrl(meta.ChannelUrl) ?? baseCandidate;
            var entry = new SavedChannelEntry
            {
                ChannelUrl = resolvedChannelUrl,
                ChannelId = meta.ChannelId ?? string.Empty,
                Name = !string.IsNullOrWhiteSpace(meta.Title) ? meta.Title : (meta.Uploader ?? "Channel"),
                Handle = meta.Uploader ?? string.Empty,
                Verified = false,
                VideoCount = meta.Entries.Count,
                RecentUploadIdsJson = ChannelUploadTracker.Compare(null, meta.Entries).Snapshot,
                SubscriberCount = meta.SubscriberCount ?? 0,
                Description = meta.Description ?? string.Empty,
                AvatarUrl = meta.ThumbnailUrl ?? string.Empty,
                AddedAt = DateTimeOffset.Now,
                LastCheckedAt = DateTimeOffset.Now
            };
            await _repository.AddAsync(entry);

            var channel = Map(entry);
            Channels.Add(channel);
            AddChannelUrl = string.Empty;
            StatusMessage = $"Added {channel.Name}.";
            await SelectChannel(channel);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static readonly (string Suffix, ChannelUploadType Type)[] ChannelTabs =
    {
        ("videos", ChannelUploadType.Video),
        ("shorts", ChannelUploadType.Short),
        ("streams", ChannelUploadType.Live),
        ("playlists", ChannelUploadType.Playlist)
    };

    [RelayCommand]
    private async Task RefreshUploads()
    {
        if (SelectedChannel is not null) await LoadUploadsAsync(SelectedChannel, forceRefresh: true);
    }

    private async Task LoadUploadsAsync(SavedChannel channel, bool forceRefresh = false)
    {
        var version = ++_uploadsLoadVersion;
        Uploads.Clear();
        if (string.IsNullOrWhiteSpace(channel.ChannelUrl))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Loading uploads…";
        try
        {
            var libraryEntries = await _library.GetAllAsync();
            var downloadedUrls = libraryEntries.Select(l => l.SourceUrl).Where(u => !string.IsNullOrEmpty(u)).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var baseUrl = StripTabSuffix(channel.ChannelUrl);
            var limit = _uploadLimit;
            var key = (baseUrl, limit, _settings.Current.Proxy, _settings.Current.CookiesFile);
            Result<PlaylistMetadata>[] tabResults;
            if (!forceRefresh && _uploadCache.TryGetValue(key, out var cached) && cached.Expires > DateTimeOffset.UtcNow)
            {
                tabResults = cached.Results;
            }
            else
            {
                _uploadCache.Remove(key);
                tabResults = await Task.WhenAll(ChannelTabs.Select(tab =>
                    _inspector.InspectPlaylistAsync($"{baseUrl}/{tab.Suffix}", limit: limit)));
                if (tabResults.Any(result => result.IsSuccess))
                {
                    if (_uploadCache.Count >= 8) _uploadCache.Remove(_uploadCache.MinBy(entry => entry.Value.Expires).Key);
                    _uploadCache[key] = (tabResults, DateTimeOffset.UtcNow.Add(
                        tabResults.All(result => result.IsSuccess) ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(30)));
                }
            }
            if (version != _uploadsLoadVersion) return;

            var anySucceeded = false;
            var merged = new List<ChannelUpload>();
            for (var i = 0; i < ChannelTabs.Length; i++)
            {
                var (_, type) = ChannelTabs[i];
                var result = tabResults[i];
                if (result.IsFailure)
                {
                    continue;
                }

                anySucceeded = true;
                foreach (var e in result.Value!.Entries)
                {
                    var isDownloaded = !string.IsNullOrEmpty(e.Url) && downloadedUrls.Contains(e.Url);
                    var upload = new ChannelUpload
                    {
                        Title = e.Title,
                        ThumbnailUrl = e.ThumbnailUrl ?? string.Empty,
                        DateText = Formatting.RelativeDate(e.UploadDate),
                        DurationText = Formatting.Duration(e.DurationSeconds),
                        ViewsText = e.ViewCount is { } v ? $"{Formatting.Count(v)} views" : string.Empty,
                        Type = type,
                        UploadDate = e.UploadDate,
                        Downloaded = isDownloaded,
                        IsSeen = isDownloaded
                    };
                    upload.Url = e.Url;
                    upload.PropertyChanged += (_, args) =>
                    {
                        if (args.PropertyName == nameof(ChannelUpload.IsSelected))
                        {
                            RaiseSelectionChanged();
                        }
                    };
                    merged.Add(upload);
                }
            }

            if (!anySucceeded)
            {
                StatusMessage = "Could not load uploads.";
                return;
            }

            foreach (var upload in merged.OrderByDescending(u => u.UploadDate ?? DateOnly.MinValue))
            {
                Uploads.Add(upload);
            }

            UpdateTabCounts(channel, merged);

            StatusMessage = string.Empty;
            RaiseSelectionChanged();
        }
        finally
        {
            if (version == _uploadsLoadVersion) IsBusy = false;
        }
    }

    private static void UpdateTabCounts(SavedChannel channel, IReadOnlyCollection<ChannelUpload> uploads)
    {
        channel.VideoCountText = FormatTabCount(uploads.Count(u => u.Type == ChannelUploadType.Video));
        channel.ShortCountText = FormatTabCount(uploads.Count(u => u.Type == ChannelUploadType.Short));
        channel.LiveCountText = FormatTabCount(uploads.Count(u => u.Type == ChannelUploadType.Live));
        channel.PlaylistCountText = FormatTabCount(uploads.Count(u => u.Type == ChannelUploadType.Playlist));
    }

    private static string FormatTabCount(int count) => count > 0 ? count.ToString() : "—";

    private static string StripTabSuffix(string channelUrl)
    {
        var trimmed = channelUrl.Trim().TrimEnd('/');
        return TabSuffixRegex().Replace(trimmed, string.Empty);
    }

    private static string? NormalizeChannelUrl(string? channelUrl) =>
        string.IsNullOrWhiteSpace(channelUrl) ? null : StripTabSuffix(channelUrl);

    [System.Text.RegularExpressions.GeneratedRegex(@"/(videos|shorts|streams|playlists|about|community)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex TabSuffixRegex();

    [RelayCommand]
    private void AddSelectedToQueue()
    {
        var selected = Uploads.Where(u => u.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Select uploads to queue.";
            return;
        }

        foreach (var upload in selected)
        {
            Enqueue(upload, start: false);
        }

        StatusMessage = $"{selected.Count} added to queue.";
        _navigation.Navigate(AppPage.Queue);
    }

    /// <summary>
    /// Queues every selected upload and starts them immediately in the current preset. Invoked by the
    /// Channels page after the user confirms the "Download selected" dialog.
    /// </summary>
    public void DownloadSelected(BatchDownloadOptions options)
    {
        var selected = Uploads.Where(u => u.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        foreach (var upload in selected)
        {
            Enqueue(upload, start: true, options);
        }

        StatusMessage = $"Downloading {selected.Count} {(selected.Count == 1 ? "video" : "videos")}…";
        _navigation.Navigate(AppPage.Queue);
    }

    [RelayCommand]
    private void DownloadLatest()
    {
        var latest = Uploads.FirstOrDefault(u => u.Type != ChannelUploadType.Live);
        if (latest is null)
        {
            StatusMessage = "No uploads to download.";
            return;
        }

        Enqueue(latest, start: true);
        _navigation.Navigate(AppPage.Queue);
    }

    [RelayCommand]
    private void DownloadUpload(ChannelUpload? upload)
    {
        if (upload is null) return;
        if (string.IsNullOrWhiteSpace(upload.Url)) { StatusMessage = "This upload has no downloadable URL."; return; }
        Enqueue(upload, start: true);
        StatusMessage = $"Downloading {upload.Title}…";
        _navigation.Navigate(AppPage.Queue);
    }

    [RelayCommand]
    private void QueueUpload(ChannelUpload? upload)
    {
        if (upload is null) return;
        if (string.IsNullOrWhiteSpace(upload.Url)) { StatusMessage = "This upload has no downloadable URL."; return; }
        Enqueue(upload, start: false);
        StatusMessage = $"Added {upload.Title} to queue.";
        _navigation.Navigate(AppPage.Queue);
    }

    private void Enqueue(ChannelUpload upload, bool start, BatchDownloadOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(upload.Url))
        {
            return;
        }

        var settings = _settings.Current;
        var isAudio = options?.IsAudio == true;
        var outputFolder = string.IsNullOrWhiteSpace(options?.OutputFolder) ? settings.DefaultDownloadFolder : options.OutputFolder;

        DownloadRequest request;
        string container;
        if (isAudio)
        {
            container = string.IsNullOrWhiteSpace(options?.Container) ? settings.PreferredAudioContainer : options!.Container;
            request = new DownloadRequest
            {
                Url = upload.Url,
                Kind = DownloadKind.Audio,
                AudioContainer = container.ToLowerInvariant(),
                AudioQuality = MapAudioQuality(options?.Quality),
                OutputFolder = outputFolder,
                FilenameTemplate = settings.FilenameTemplate,
                EmbedMetadata = true,
                EmbedThumbnail = true,
                CookiesFile = settings.CookiesFile,
                Proxy = settings.Proxy,
                SpeedLimitKbps = settings.DownloadSpeedLimitKbps,
                KnownTitle = upload.Title
            };
        }
        else
        {
            container = string.IsNullOrWhiteSpace(options?.Container) ? settings.PreferredVideoContainer : options!.Container;
            var embedSubtitles = !string.IsNullOrWhiteSpace(options?.SubtitleLanguage) && options.SubtitleLanguage != "None";
            request = new DownloadRequest
            {
                Url = upload.Url,
                Kind = DownloadKind.Video,
                MaxHeight = ParseHeight(options?.Quality),
                VideoContainer = container.ToLowerInvariant(),
                AudioContainer = settings.PreferredAudioContainer,
                OutputFolder = outputFolder,
                FilenameTemplate = settings.FilenameTemplate,
                EmbedMetadata = true,
                EmbedSubtitles = embedSubtitles,
                SubtitleLanguages = MapSubtitleLang(options?.SubtitleLanguage),
                CookiesFile = settings.CookiesFile,
                Proxy = settings.Proxy,
                SpeedLimitKbps = settings.DownloadSpeedLimitKbps,
                KnownTitle = upload.Title
            };
        }

        var qualityLabel = string.IsNullOrWhiteSpace(options?.Quality) ? "Best" : options.Quality;
        _coordinator.Enqueue(request, new EnqueueInfo(upload.Title, SelectedChannel?.Name ?? string.Empty, qualityLabel, container.ToUpperInvariant(), upload.ThumbnailUrl), start);
        upload.Downloaded = true;
        upload.IsSeen = true;
    }

    private static string MapAudioQuality(string? label) => label switch
    {
        null or "Best" => "0",
        _ => new string(label.TakeWhile(char.IsDigit).ToArray()) is { Length: > 0 } k ? $"{k}K" : "0"
    };

    private static int ParseHeight(string? quality)
    {
        if (string.IsNullOrWhiteSpace(quality))
        {
            return 0;
        }

        var digits = new string(quality.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var h) ? h : 0;
    }

    private static string MapSubtitleLang(string? label) => label switch
    {
        null or "None" or "English" => "en.*",
        "All" => ".*",
        "Spanish" => "es.*",
        "French" => "fr.*",
        "German" => "de.*",
        "Japanese" => "ja.*",
        _ => $"{label}.*"
    };

    [RelayCommand]
    private void SelectUnseen()
    {
        foreach (var upload in Uploads)
        {
            upload.IsSelected = !upload.IsSeen && !upload.Downloaded;
        }
        RaiseSelectionChanged();
    }

    [RelayCommand]
    private void SelectAll()
    {
        var allSelected = Uploads.Count > 0 && Uploads.All(u => u.IsSelected);
        foreach (var upload in Uploads)
        {
            upload.IsSelected = !allSelected;
        }
        RaiseSelectionChanged();
    }

    [RelayCommand]
    private void MarkAllAsSeen()
    {
        foreach (var upload in Uploads)
        {
            upload.IsSeen = true;
        }
        StatusMessage = "All uploads marked as seen.";
    }

    [RelayCommand]
    private void ToggleSeen(ChannelUpload? upload)
    {
        if (upload is not null)
        {
            upload.IsSeen = !upload.IsSeen;
        }
    }

    [RelayCommand]
    private async Task UnfollowChannel()
    {
        if (SelectedChannel is null)
        {
            return;
        }

        if (SelectedChannel.EntryId > 0)
        {
            await _repository.DeleteAsync(SelectedChannel.EntryId);
        }

        Channels.Remove(SelectedChannel);
        Uploads.Clear();
        SelectedChannel = Channels.FirstOrDefault();
        if (SelectedChannel is not null)
        {
            await LoadUploadsAsync(SelectedChannel);
        }
    }

    [RelayCommand]
    private void OpenChannel()
    {
        if (!string.IsNullOrWhiteSpace(SelectedChannel?.ChannelUrl))
        {
            _system.OpenUrl(SelectedChannel.ChannelUrl);
        }
    }

    public ObservableCollection<int> CheckFrequencyOptions { get; } = new() { 1, 3, 6, 12, 24 };

    [RelayCommand]
    private async Task SetCheckFrequency(int hours)
    {
        if (SelectedChannel is null || hours <= 0)
        {
            return;
        }

        SelectedChannel.CheckFrequencyHours = hours;
        ApplyCheckFrequency(SelectedChannel);

        if (SelectedChannel.EntryId > 0)
        {
            var entries = await _repository.GetAllAsync();
            var entry = entries.FirstOrDefault(x => x.Id == SelectedChannel.EntryId);
            if (entry is not null)
            {
                entry.CheckFrequencyHours = hours;
                await _repository.UpdateAsync(entry);
            }
        }
    }

    private static void ApplyCheckFrequency(SavedChannel channel)
    {
        channel.CheckFrequencyText = FormatFrequency(channel.CheckFrequencyHours);
        channel.NextCheckText = ComputeNextCheck(channel);
    }

    internal static string FormatFrequency(int hours) => hours switch
    {
        1 => "1 hour",
        24 => "24 hours (daily)",
        _ => $"{hours} hours"
    };

    private static string ComputeNextCheck(SavedChannel channel) =>
        channel.LastCheckedAtRaw is { } last ? Formatting.ShortDate(last.AddHours(channel.CheckFrequencyHours)) : "Pending";

    private static SavedChannel Map(SavedChannelEntry e)
    {
        var channel = new SavedChannel
        {
            EntryId = e.Id,
            ChannelUrl = e.ChannelUrl,
            Name = e.Name,
            Handle = e.Handle,
            Initial = string.IsNullOrEmpty(e.Name) ? "?" : e.Name[..1].ToUpperInvariant(),
            AvatarUrl = e.AvatarUrl,
            Verified = e.Verified,
            SubscriberCountText = e.SubscriberCount > 0 ? Formatting.Count(e.SubscriberCount) : "—",
            VideoCountText = e.VideoCount > 0 ? e.VideoCount.ToString() : "—",
            ShortCountText = "—",
            LiveCountText = "—",
            PlaylistCountText = "—",
            TotalViewsText = "—",
            Description = e.Description,
            CountryText = string.IsNullOrWhiteSpace(e.Country) ? "—" : e.Country,
            LanguageText = "—",
            JoinedText = "—",
            LastCheckedAtRaw = e.LastCheckedAt,
            LastCheckedText = Formatting.ShortDate(e.LastCheckedAt),
            CheckFrequencyHours = e.CheckFrequencyHours <= 0 ? 6 : e.CheckFrequencyHours
        };
        ApplyCheckFrequency(channel);
        return channel;
    }
}
