using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVault.Presentation.ViewModels.Models;

namespace StreamVault.Presentation.ViewModels;

/// <summary>Backs the Playlists page (spec section 7.3): inspect and batch-download a playlist.</summary>
public partial class PlaylistsViewModel : ObservableObject
{
    private readonly ICollectionView _filteredEntries;

    public PlaylistsViewModel()
    {
        LoadSampleData();
        _filteredEntries = CollectionViewSource.GetDefaultView(Entries);
        _filteredEntries.Filter = FilterEntry;
        foreach (var entry in Entries)
        {
            entry.PropertyChanged += OnEntryPropertyChanged;
        }
    }

    [ObservableProperty] private string _playlistUrl = "https://www.youtube.com/playlist?list=PLscenic";
    [ObservableProperty] private string _playlistTitle = "Switzerland 4K \u2013 Scenic Relaxation Film with Calming Music";
    [ObservableProperty] private string _creator = "Scenic Relaxation";
    [ObservableProperty] private string _videoCountText = "54 videos";
    [ObservableProperty] private string _totalDurationText = "4h 38m";
    [ObservableProperty] private string _visibility = "Public";
    [ObservableProperty] private string _description = "Experience the breathtaking beauty of Switzerland in stunning 4K. Enjoy scenic landscapes, relaxing music, and peaceful natural...";

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private PlaylistFilter _selectedFilter = PlaylistFilter.All;

    [ObservableProperty] private bool _skipAlreadyDownloaded = true;
    [ObservableProperty] private bool _skipPrivateVideos = true;
    [ObservableProperty] private bool _skipUnavailableVideos;

    [ObservableProperty] private string _commonFormat = "MP4";
    [ObservableProperty] private string _videoQuality = "Best Quality (4K)";
    [ObservableProperty] private string _audioQuality = "320 kbps";
    [ObservableProperty] private string _subtitleLanguage = "English (auto)";
    [ObservableProperty] private bool _embedSubtitles = true;
    [ObservableProperty] private string _namingTemplate = "%(playlist)s - %(index)02d - %(title)s";
    [ObservableProperty] private string _outputFolder = @"C:\Users\Alex\Videos\StreamVault";
    [ObservableProperty] private bool _createSubfolder = true;
    [ObservableProperty] private string _numberingPattern = "01, 02, 03 ... (Sequential)";

    public ObservableCollection<PlaylistEntry> Entries { get; } = new();

    public ICollectionView FilteredEntries => _filteredEntries;

    // Playlist-wide stats (the full remote playlist has 54 entries; only a page of them is loaded into Entries for the sample table).
    public int TotalCount => 54;
    public int AvailableCount => 50;
    public int UnavailableCount => 2;
    public int SelectedCount => Entries.Count(e => e.IsSelected);
    public string RequiredSpaceText => "21.3 GB";
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
    private void SelectAll() { foreach (var e in Entries) e.IsSelected = true; }

    [RelayCommand]
    private void ClearSelection() { foreach (var e in Entries) e.IsSelected = false; }

    [RelayCommand]
    private void InvertSelection() { foreach (var e in Entries) e.IsSelected = !e.IsSelected; }

    [RelayCommand] private void DownloadSelected() { }
    [RelayCommand] private void AddSelectedToQueue() { }

    private void LoadSampleData()
    {
        (string Title, string Duration, PlaylistEntryAvailability Availability)[] rows =
        {
            ("Lauterbrunnen Valley \u2013 4K Drone Footage | Switzerland", "10:47", PlaylistEntryAvailability.Available),
            ("Zermatt & Matterhorn \u2013 Stunning Views in 4K", "08:35", PlaylistEntryAvailability.Available),
            ("Relaxing Walk in Interlaken \u2013 4K Nature & City Sounds", "12:21", PlaylistEntryAvailability.Available),
            ("Swiss Alps in Autumn \u2013 Golden Larches & Mountains", "07:58", PlaylistEntryAvailability.Unavailable),
            ("Lucerne \u2013 Beautiful City by the Lake", "09:14", PlaylistEntryAvailability.Available),
            ("Grindelwald \u2013 The Perfect Alpine Village", "06:43", PlaylistEntryAvailability.Available),
            ("Relaxing Piano Music with Swiss Landscapes", "1:02:15", PlaylistEntryAvailability.Available),
            ("Top of Europe \u2013 Jungfraujoch 4K Experience", "11:36", PlaylistEntryAvailability.Available),
        };

        for (var i = 0; i < rows.Length; i++)
        {
            Entries.Add(new PlaylistEntry
            {
                Index = i + 1,
                Title = rows[i].Title,
                Duration = rows[i].Duration,
                Channel = "Scenic Relaxation",
                Availability = rows[i].Availability,
                Format = rows[i].Availability == PlaylistEntryAvailability.Available ? "Best (4K)" : "N/A",
                IsSelected = i == 0
            });
        }
    }
}
