using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVault.Application.Abstractions;
using StreamVault.Application.Downloads;
using StreamVault.Domain.Media;
using StreamVault.Presentation.Navigation;
using StreamVault.Presentation.Services;
using StreamVault.Presentation.ViewModels.Models;
using StreamVault.Shared;
using PlaylistEntry = StreamVault.Presentation.ViewModels.Models.PlaylistEntry;

namespace StreamVault.Presentation.ViewModels;

/// <summary>Backs the Playlists page: inspect a playlist and batch-download/queue selected entries.</summary>
public partial class PlaylistsViewModel : ObservableObject
{
    private readonly IMediaInspector _inspector;
    private readonly DownloadCoordinator _coordinator;
    private readonly ISettingsService _settings;
    private readonly ISystemAccess _system;
    private readonly INavigationService _navigation;
    private readonly ILibraryRepository _library;
    private readonly ICollectionView _filteredEntries;

    public PlaylistsViewModel(
        IMediaInspector inspector,
        DownloadCoordinator coordinator,
        ISettingsService settings,
        ISystemAccess system,
        INavigationService navigation,
        ILibraryRepository library)
    {
        _inspector = inspector;
        _coordinator = coordinator;
        _settings = settings;
        _system = system;
        _navigation = navigation;
        _library = library;

        _outputFolder = settings.Current.DefaultDownloadFolder;
        _filteredEntries = CollectionViewSource.GetDefaultView(Entries);
        _filteredEntries.Filter = FilterEntry;
    }

    [ObservableProperty] private string _playlistUrl = string.Empty;
    [ObservableProperty] private string _playlistTitle = "No playlist loaded";
    [ObservableProperty] private string _creator = string.Empty;
    [ObservableProperty] private string _videoCountText = "0 videos";
    [ObservableProperty] private string _totalDurationText = "—";
    [ObservableProperty] private string _visibility = "—";
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _isInspecting;
    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private PlaylistFilter _selectedFilter = PlaylistFilter.All;

    [ObservableProperty] private bool _skipAlreadyDownloaded = true;
    [ObservableProperty] private bool _skipPrivateVideos = true;
    [ObservableProperty] private bool _skipUnavailableVideos;

    [ObservableProperty] private string _commonFormat = "MP4";
    [ObservableProperty] private string _videoQuality = "Best Quality";
    [ObservableProperty] private string _audioQuality = "Best";
    [ObservableProperty] private string _subtitleLanguage = "English";
    [ObservableProperty] private bool _embedSubtitles = true;
    [ObservableProperty] private string _namingTemplate = "%(playlist_index)02d - %(title)s [%(id)s].%(ext)s";
    [ObservableProperty] private string _outputFolder = string.Empty;
    [ObservableProperty] private bool _createSubfolder = true;
    [ObservableProperty] private string _numberingPattern = "Sequential (01, 02, 03)";

    public ObservableCollection<PlaylistEntry> Entries { get; } = new();
    public ICollectionView FilteredEntries => _filteredEntries;

    public ObservableCollection<string> FormatOptions { get; } = new() { "MP4", "MKV", "WEBM" };
    public ObservableCollection<string> VideoQualityOptions { get; } =
        new() { "Best Quality", "2160p", "1440p", "1080p", "720p", "480p", "360p" };
    public ObservableCollection<string> AudioQualityOptions { get; } =
        new() { "Best", "320 kbps", "256 kbps", "192 kbps", "128 kbps" };
    public ObservableCollection<string> SubtitleOptions { get; } = new() { "None", "English" };
    public ObservableCollection<string> NumberingOptions { get; } =
        new() { "Sequential (01, 02, 03)", "Plain (1, 2, 3)", "None" };

    public int TotalCount => Entries.Count;
    public int AvailableCount => Entries.Count(e => e.Availability == PlaylistEntryAvailability.Available);
    public int UnavailableCount => Entries.Count(e => e.Availability != PlaylistEntryAvailability.Available);
    public int SelectedCount => Entries.Count(e => e.IsSelected);
    public string RequiredSpaceText => "—";
    public string EstimatedDurationText => TotalDurationText;

    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaylistEntry.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
        }
    }

    private bool FilterEntry(object obj)
    {
        if (obj is not PlaylistEntry entry)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchText) && !entry.Title.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return SelectedFilter switch
        {
            PlaylistFilter.Available => entry.Availability == PlaylistEntryAvailability.Available,
            PlaylistFilter.Unavailable => entry.Availability != PlaylistEntryAvailability.Available,
            PlaylistFilter.Selected => entry.IsSelected,
            _ => true
        };
    }

    [RelayCommand]
    private void SelectFilter(PlaylistFilter filter)
    {
        SelectedFilter = filter;
        _filteredEntries.Refresh();
    }

    partial void OnSearchTextChanged(string value) => _filteredEntries.Refresh();

    [RelayCommand]
    private async Task Inspect()
    {
        if (string.IsNullOrWhiteSpace(PlaylistUrl))
        {
            StatusMessage = "Paste a playlist URL first.";
            return;
        }

        IsInspecting = true;
        StatusMessage = "Inspecting playlist…";
        try
        {
            var result = await _inspector.InspectPlaylistAsync(PlaylistUrl);
            if (result.IsFailure)
            {
                StatusMessage = result.Error ?? "Playlist inspection failed.";
                return;
            }

            ApplyPlaylist(result.Value!);
            StatusMessage = string.Empty;
        }
        finally
        {
            IsInspecting = false;
        }
    }

    [RelayCommand]
    private void Paste()
    {
        var text = _system.ReadClipboardText();
        if (!string.IsNullOrWhiteSpace(text))
        {
            PlaylistUrl = text.Trim();
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var e in Entries)
        {
            e.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var e in Entries)
        {
            e.IsSelected = false;
        }
    }

    [RelayCommand]
    private void InvertSelection()
    {
        foreach (var e in Entries)
        {
            e.IsSelected = !e.IsSelected;
        }
    }

    [RelayCommand]
    private async Task DownloadSelected() => await EnqueueSelected(start: true);

    [RelayCommand]
    private async Task AddSelectedToQueue() => await EnqueueSelected(start: false);

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
    private void InsertTemplateVariable(string token) => NamingTemplate += token;

    private async Task EnqueueSelected(bool start)
    {
        var selected = Entries.Where(e => e.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Select at least one video.";
            return;
        }

        HashSet<string> already = new(StringComparer.OrdinalIgnoreCase);
        if (SkipAlreadyDownloaded)
        {
            var lib = await _library.GetAllAsync();
            already = lib.Select(l => l.SourceUrl).Where(u => !string.IsNullOrWhiteSpace(u))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var settings = _settings.Current;
        var folder = OutputFolder;
        if (CreateSubfolder && PlaylistTitle is not ("No playlist loaded" or ""))
        {
            folder = Path.Combine(OutputFolder, Sanitize(PlaylistTitle));
        }

        var enqueued = 0;
        foreach (var entry in selected)
        {
            if (SkipUnavailableVideos && entry.Availability == PlaylistEntryAvailability.Unavailable)
            {
                continue;
            }

            if (SkipPrivateVideos && entry.Availability == PlaylistEntryAvailability.Private)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.Url) && already.Contains(entry.Url))
            {
                continue;
            }

            var request = new DownloadRequest
            {
                Url = entry.Url,
                Kind = DownloadKind.Video,
                MaxHeight = ParseHeight(VideoQuality),
                VideoContainer = CommonFormat.ToLowerInvariant(),
                AudioContainer = settings.PreferredAudioContainer,
                OutputFolder = folder,
                FilenameTemplate = "%(title)s [%(id)s].%(ext)s",
                EmbedSubtitles = EmbedSubtitles && SubtitleLanguage != "None",
                EmbedMetadata = true,
                SubtitleLanguages = SubtitleLanguage == "None" ? "en.*" : $"{MapLang(SubtitleLanguage)}.*",
                CookiesFile = settings.CookiesFile,
                Proxy = settings.Proxy,
                SpeedLimitKbps = settings.DownloadSpeedLimitKbps,
                KnownTitle = entry.Title
            };

            _coordinator.Enqueue(request, new EnqueueInfo(entry.Title, entry.Channel, VideoQuality, CommonFormat), start);
            enqueued++;
        }

        StatusMessage = $"{enqueued} added to queue.";
        if (enqueued > 0)
        {
            _navigation.Navigate(AppPage.Queue);
        }
    }

    private void ApplyPlaylist(PlaylistMetadata m)
    {
        foreach (var e in Entries)
        {
            e.PropertyChanged -= OnEntryPropertyChanged;
        }

        Entries.Clear();

        PlaylistTitle = m.Title;
        Creator = m.Uploader ?? string.Empty;
        Visibility = string.IsNullOrWhiteSpace(m.Availability) ? "Public" : Capitalize(m.Availability!);
        Description = m.Description ?? string.Empty;
        VideoCountText = $"{m.Entries.Count} videos";

        var totalSeconds = m.Entries.Sum(e => e.DurationSeconds ?? 0);
        TotalDurationText = Formatting.Eta(totalSeconds);

        var i = 0;
        foreach (var e in m.Entries)
        {
            i++;
            var entry = new PlaylistEntry
            {
                Index = i,
                Title = e.Title,
                Duration = Formatting.Duration(e.DurationSeconds),
                Channel = e.Uploader ?? m.Uploader ?? string.Empty,
                Availability = MapAvailability(e.Availability),
                Format = "Best",
                IsSelected = true
            };
            entry.Url = e.Url;
            entry.PropertyChanged += OnEntryPropertyChanged;
            Entries.Add(entry);
        }

        _filteredEntries.Refresh();
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(AvailableCount));
        OnPropertyChanged(nameof(UnavailableCount));
        OnPropertyChanged(nameof(SelectedCount));
    }

    private static PlaylistEntryAvailability MapAvailability(string availability) => availability.ToLowerInvariant() switch
    {
        "available" or "public" or "unlisted" => PlaylistEntryAvailability.Available,
        "private" or "needs_auth" or "premium_only" or "subscriber_only" => PlaylistEntryAvailability.Private,
        _ => PlaylistEntryAvailability.Unavailable
    };

    private static int ParseHeight(string quality)
    {
        var digits = new string(quality.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var h) ? h : 0;
    }

    private static string MapLang(string label) => label == "English" ? "en" : label;

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        return name.Trim();
    }
}
