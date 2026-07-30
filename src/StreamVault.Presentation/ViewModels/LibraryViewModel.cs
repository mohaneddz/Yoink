using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVault.Application.Abstractions;
using StreamVault.Domain.Entities;
using StreamVault.Presentation.Services;
using StreamVault.Presentation.ViewModels.Models;
using StreamVault.Shared;

namespace StreamVault.Presentation.ViewModels;

/// <summary>Backs the Library page: browse, search, and manage downloaded media from the store.</summary>
public partial class LibraryViewModel : ObservableObject
{
    private readonly ILibraryRepository _repository;
    private readonly ISystemAccess _system;
    private readonly IFfmpegService _ffmpeg;
    private readonly List<LibraryItem> _all = new();

    public LibraryViewModel(ILibraryRepository repository, ISystemAccess system, IFfmpegService ffmpeg)
    {
        _repository = repository;
        _system = system;
        _ffmpeg = ffmpeg;
        _ = LoadAsync();
    }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _sortBy = "Date Downloaded (Newest)";
    [ObservableProperty] private LibraryMediaFilter _selectedFilter = LibraryMediaFilter.Video;
    [ObservableProperty] private LibraryViewMode _viewMode = LibraryViewMode.Grid;
    [ObservableProperty] private string _statusMessage = string.Empty;

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

    public async Task LoadAsync()
    {
        var entries = await _repository.GetAllAsync();
        _all.Clear();
        foreach (var e in entries)
        {
            _all.Add(Map(e));
        }

        BuildGroups();
        ApplyView();
    }

    private void ApplyView()
    {
        IEnumerable<LibraryItem> query = _all.Where(i => MatchesFilter(i.Kind, SelectedFilter));

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
            "Date Downloaded (Oldest)" => query.OrderBy(i => i.DownloadDateText),
            "Title (A–Z)" => query.OrderBy(i => i.Title),
            "Title (Z–A)" => query.OrderByDescending(i => i.Title),
            "File Size (Largest)" => query.OrderByDescending(i => i.FileSizeText),
            _ => query
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
    }

    private void BuildGroups()
    {
        Channels.Clear();
        foreach (var group in _all.Where(i => !string.IsNullOrWhiteSpace(i.Channel))
                     .GroupBy(i => i.Channel).OrderByDescending(g => g.Count()))
        {
            Channels.Add(new LibraryGroupSummary
            {
                Name = group.Key,
                ItemCountText = $"{group.Count()} item{(group.Count() == 1 ? "" : "s")}",
                UpdatedText = string.Empty,
                IconKey = "IconUser"
            });
        }

        Playlists.Clear();
        foreach (var group in _all.Where(i => !string.IsNullOrWhiteSpace(i.FilePath))
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
        if (SelectedItem is not null)
        {
            _system.PlayFile(SelectedItem.FilePath);
        }
    }

    [RelayCommand]
    private void OpenFile()
    {
        if (SelectedItem is not null)
        {
            _system.PlayFile(SelectedItem.FilePath);
        }
    }

    [RelayCommand]
    private void RevealInFolder()
    {
        if (SelectedItem is not null)
        {
            _system.RevealInExplorer(SelectedItem.FilePath);
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
        RecentDownloads.Remove(SelectedItem);
        BuildGroups();
    }

    private static LibraryItem Map(LibraryEntry e)
    {
        var item = new LibraryItem
        {
            EntryId = e.Id,
            Kind = e.Kind,
            Title = e.Title,
            Channel = e.Channel,
            DurationText = Formatting.Duration(e.DurationSeconds),
            Format = string.IsNullOrWhiteSpace(e.Format) ? "—" : e.Format,
            Quality = string.IsNullOrWhiteSpace(e.Quality) ? "—" : e.Quality,
            AudioCodec = string.IsNullOrWhiteSpace(e.AudioCodec) ? "—" : e.AudioCodec,
            VideoCodec = string.IsNullOrWhiteSpace(e.VideoCodec) ? "—" : e.VideoCodec,
            DownloadDateText = Formatting.ShortDate(e.DownloadedAt),
            FileSizeText = Formatting.Bytes(e.FileSizeBytes),
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
}
