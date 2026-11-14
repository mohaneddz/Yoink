using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yoink.Application.Abstractions;
using Yoink.Domain.Entities;
using Yoink.Presentation.Services;
using Yoink.Presentation.ViewModels.Models;
using Yoink.Shared;

namespace Yoink.Presentation.ViewModels;

/// <summary>Backs the Library page: browse, search, and manage downloaded media from the store.</summary>
public partial class LibraryViewModel : ObservableObject
{
    private readonly ILibraryRepository _repository;
    private readonly ISavedChannelRepository _channels;
    private readonly ISystemAccess _system;
    private readonly IFfmpegService _ffmpeg;
    private readonly ISettingsService _settings;
    private readonly List<LibraryItem> _all = new();
    private int _loadVersion;

    public LibraryViewModel(
        ILibraryRepository repository,
        ISavedChannelRepository channels,
        ISystemAccess system,
        IFfmpegService ffmpeg, ISettingsService settings)
    {
        _repository = repository;
        _channels = channels;
        _system = system;
        _ffmpeg = ffmpeg;
        _settings = settings;
        settings.Changed += (_, _) => UiDispatcher.Run(() => { ApplyView(); _ = BuildGroupsAsync(); });
        _ = LoadAsync();
    }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _sortBy = "Date Downloaded (Newest)";
    [ObservableProperty] private LibraryMediaFilter _selectedFilter = LibraryMediaFilter.Video;
    [ObservableProperty] private LibraryViewMode _viewMode = LibraryViewMode.Grid;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _emptyMessage = "Downloaded media will appear here.";

    public ObservableCollection<LibraryItem> RecentDownloads { get; } = new();
    public ObservableCollection<LibraryGroupSummary> Playlists { get; } = new();
    public ObservableCollection<LibraryGroupSummary> Channels { get; } = new();
    public ObservableCollection<string> SortOptions { get; } = new()
    {
        "Date Downloaded (Newest)", "Date Downloaded (Oldest)", "Title (A–Z)", "Title (Z–A)", "File Size (Largest)"
    };

    [ObservableProperty] private LibraryItem? _selectedItem;

    partial void OnSearchTextChanged(string value) => ApplyView();
    partial void OnSortByChanged(string value) => ApplyView();
    partial void OnSelectedFilterChanged(LibraryMediaFilter value) => ApplyView();

    [RelayCommand] private void SelectMediaFilter(LibraryMediaFilter filter) => SelectedFilter = filter;
    [RelayCommand] private void SetViewMode(LibraryViewMode mode) => ViewMode = mode;
    [RelayCommand] private void SelectItem(LibraryItem item) => SelectedItem = item;
    [RelayCommand] private void ClearSearch() => SearchText = string.Empty;
    [RelayCommand] private async Task Refresh() => await LoadAsync();

    public async Task LoadAsync()
    {
        var version = ++_loadVersion;
        IsLoading = true;
        StatusMessage = "Scanning library…";
        try
        {
            var entries = await _repository.GetAllAsync();
            if (version != _loadVersion) return;
            _all.Clear();
            foreach (var e in entries) _all.Add(Map(e));
            await BuildGroupsAsync();
            if (version != _loadVersion) return;
            ApplyView();
            StatusMessage = $"Library ready · {_all.Count} item{(_all.Count == 1 ? "" : "s")}.";
        }
        catch (Exception ex)
        {
            if (version != _loadVersion) return;
            StatusMessage = $"Could not load library: {ex.Message}";
            EmptyMessage = "Could not load the library. Use Rescan library to try again.";
        }
        finally { if (version == _loadVersion) IsLoading = false; }
    }

    private bool IsVisible(LibraryItem item) => !_settings.Current.HidePrivateVideos
        || !Yoink.Domain.Media.SourcePrivacy.IsPrivateSource(item.SourceUrl);

    private void ApplyView()
    {
        IEnumerable<LibraryItem> query = _all.Where(IsVisible).Where(i => MatchesFilter(i.Kind, SelectedFilter));

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            query = query.Where(i =>
                i.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Channel.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        query = SortBy switch
        {
            "Date Downloaded (Oldest)" => query.OrderBy(i => i.DownloadedAt),
            "Title (A–Z)" => query.OrderBy(i => i.Title),
            "Title (Z–A)" => query.OrderByDescending(i => i.Title),
            "File Size (Largest)" => query.OrderByDescending(i => i.FileSizeBytes),
            _ => query.OrderByDescending(i => i.DownloadedAt)
        };

        RecentDownloads.Clear();
        foreach (var i in query)
        {
            RecentDownloads.Add(i);
        }

        if (SelectedItem is null || !RecentDownloads.Contains(SelectedItem))
        {
            SelectedItem = RecentDownloads.FirstOrDefault();
        }
        EmptyMessage = _all.Count == 0
            ? "Downloaded media will appear here once a download completes."
            : "No media matches these filters. Try another type or clear the search.";
    }

    private async Task BuildGroupsAsync()
    {
        var savedChannels = await _channels.GetAllAsync();
        var channelMap = savedChannels.ToDictionary(c => c.Name, c => c.AvatarUrl, StringComparer.OrdinalIgnoreCase);

        Channels.Clear();
        foreach (var group in _all.Where(IsVisible).Where(i => !string.IsNullOrWhiteSpace(i.Channel))
                     .GroupBy(i => i.Channel).OrderByDescending(g => g.Count()))
        {
            channelMap.TryGetValue(group.Key, out var avatarUrl);
            Channels.Add(new LibraryGroupSummary
            {
                Name = group.Key,
                ItemCountText = $"{group.Count()} item{(group.Count() == 1 ? "" : "s")}",
                UpdatedText = string.Empty,
                IconKey = "IconUser",
                AvatarUrl = avatarUrl ?? string.Empty
            });
        }

        foreach (var item in _all)
        {
            if (channelMap.TryGetValue(item.Channel, out var av) && !string.IsNullOrWhiteSpace(av))
            {
                item.ChannelAvatarUrl = av;
            }
        }

        Playlists.Clear();
        foreach (var group in _all.Where(IsVisible).Where(i => !string.IsNullOrWhiteSpace(i.FilePath))
                     .GroupBy(i => Path.GetFileName(Path.GetDirectoryName(i.FilePath) ?? string.Empty))
                     .Where(g => !string.IsNullOrWhiteSpace(g.Key))
                     .OrderByDescending(g => g.Count()))
        {
            Playlists.Add(new LibraryGroupSummary
            {
                Name = group.Key,
                ItemCountText = $"{group.Count()} item{(group.Count() == 1 ? "" : "s")}",
                UpdatedText = string.Empty,
                IconKey = "IconFolder"
            });
        }
    }

    private static bool MatchesFilter(DownloadKind kind, LibraryMediaFilter filter) => filter switch
    {
        LibraryMediaFilter.Video => kind == DownloadKind.Video,
        LibraryMediaFilter.Audio => kind == DownloadKind.Audio,
        LibraryMediaFilter.Subtitles => kind == DownloadKind.Subtitles,
        LibraryMediaFilter.Thumbnails => kind == DownloadKind.Thumbnail,
        _ => true
    };

    [RelayCommand]
    private void Play()
    {
        OpenSelectedFile();
    }

    [RelayCommand]
    private void OpenFile()
    {
        OpenSelectedFile();
    }

    private void OpenSelectedFile()
    {
        if (SelectedItem is null) return;
        if (!File.Exists(SelectedItem.FilePath))
        {
            SelectedItem.IsMissing = true;
            StatusMessage = "File is missing. Reveal its folder or remove this record from the library.";
            return;
        }
        _system.PlayFile(SelectedItem.FilePath);
    }

    [RelayCommand]
    private void RevealInFolder()
    {
        if (SelectedItem is not null)
        {
            _system.RevealInExplorer(SelectedItem.FilePath);
            if (!File.Exists(SelectedItem.FilePath))
                StatusMessage = "File is missing. Opened its original folder if it still exists.";
        }
    }

    [RelayCommand]
    private void CopySourceUrl()
    {
        if (!string.IsNullOrWhiteSpace(SelectedItem?.SourceUrl))
        {
            _system.SetClipboardText(SelectedItem.SourceUrl);
        }
    }

    [RelayCommand]
    private async Task ConvertVideo()
    {
        if (SelectedItem is null || !File.Exists(SelectedItem.FilePath))
        {
            StatusMessage = "Select an existing file before converting.";
            return;
        }

        var current = Path.GetExtension(SelectedItem.FilePath).TrimStart('.').ToLowerInvariant();
        var target = current == "mp4" ? "mkv" : "mp4";
        StatusMessage = $"Converting to {target.ToUpperInvariant()}…";

        var result = await _ffmpeg.ConvertAsync(SelectedItem.FilePath, target);
        if (result.IsSuccess && result.Value is not null)
        {
            var probe = await _ffmpeg.ProbeAsync(result.Value);
            await _repository.UpsertAsync(new LibraryEntry
            {
                FilePath = result.Value,
                Title = SelectedItem.Title,
                Channel = SelectedItem.Channel,
                SourceUrl = SelectedItem.SourceUrl,
                Kind = SelectedItem.Kind,
                Format = target.ToUpperInvariant(),
                Quality = SelectedItem.Quality,
                ResolutionText = SelectedItem.ResolutionText,
                DurationSeconds = probe?.DurationSeconds,
                FileSizeBytes = probe?.SizeBytes ?? 0,
                DownloadedAt = DateTimeOffset.Now
            });
            StatusMessage = "Conversion complete.";
            await LoadAsync();
        }
        else
        {
            StatusMessage = result.Error ?? "Conversion failed.";
        }
    }

    [RelayCommand]
    private async Task RemoveFromLibrary()
    {
        if (SelectedItem is null)
        {
            return;
        }

        if (SelectedItem.EntryId > 0)
        {
            await _repository.DeleteAsync(SelectedItem.EntryId);
        }

        _all.Remove(SelectedItem);
        ApplyView();
        await BuildGroupsAsync();
        StatusMessage = "Removed the record from your library. The file was not deleted.";
    }

    [RelayCommand]
    private async Task RemoveSelectedRecords()
    {
        var selected = RecentDownloads.Where(i => i.IsSelected).ToArray();
        if (selected.Length == 0) { StatusMessage = "Select items to remove first."; return; }
        foreach (var item in selected)
        {
            if (item.EntryId > 0) await _repository.DeleteAsync(item.EntryId);
            _all.Remove(item);
        }
        ApplyView();
        await BuildGroupsAsync();
        StatusMessage = $"Removed {selected.Length} library record{(selected.Length == 1 ? "" : "s")}. Files were not deleted.";
    }

    private static LibraryItem Map(LibraryEntry e)
    {
        var thumb = TryExtractThumbnailUrl(e.SourceUrl, e.ThumbnailUrl);
        var item = new LibraryItem
        {
            EntryId = e.Id,
            Kind = e.Kind,
            Title = e.Title,
            Channel = e.Channel,
            ThumbnailUrl = thumb,
            ThumbnailHex = ColorFromKey(e.Title),
            DurationText = Formatting.Duration(e.DurationSeconds),
            Format = string.IsNullOrWhiteSpace(e.Format) ? "—" : e.Format,
            Quality = string.IsNullOrWhiteSpace(e.Quality) ? "—" : e.Quality,
            AudioCodec = string.IsNullOrWhiteSpace(e.AudioCodec) ? "—" : e.AudioCodec,
            VideoCodec = string.IsNullOrWhiteSpace(e.VideoCodec) ? "—" : e.VideoCodec,
            DownloadDateText = Formatting.ShortDate(e.DownloadedAt),
            DownloadedAt = e.DownloadedAt,
            FileSizeText = Formatting.Bytes(e.FileSizeBytes),
            FileSizeBytes = e.FileSizeBytes,
            FilePath = e.FilePath,
            ResolutionText = string.IsNullOrWhiteSpace(e.ResolutionText) ? "—" : e.ResolutionText,
            FrameRateText = e.Fps is { } f ? $"{f:0} FPS" : "—",
            SourceUrl = e.SourceUrl,
            IsMissing = !File.Exists(e.FilePath)
        };

        foreach (var tag in (e.TagsCsv ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            item.Tags.Add(tag);
        }

        return item;
    }

    private static string TryExtractThumbnailUrl(string? sourceUrl, string? explicitThumb)
    {
        if (!string.IsNullOrWhiteSpace(explicitThumb))
        {
            return explicitThumb;
        }

        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return string.Empty;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            sourceUrl,
            @"(?:v=|\/v\/|\/embed\/|\/shorts\/|youtu\.be\/|\/watch\?v=)([a-zA-Z0-9_-]{11})");

        if (match.Success)
        {
            return $"https://i.ytimg.com/vi/{match.Groups[1].Value}/hqdefault.jpg";
        }

        return string.Empty;
    }

    private static string ColorFromKey(string key)
    {
        string[] palette = { "#2B3A4A", "#1E3A34", "#3D2C4A", "#4A2B2B", "#2D3748", "#1F2937" };
        if (string.IsNullOrEmpty(key)) return palette[0];
        var hash = Math.Abs(key.GetHashCode());
        return palette[hash % palette.Length];
    }
}
