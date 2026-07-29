using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVault.Presentation.ViewModels.Models;

namespace StreamVault.Presentation.ViewModels;

/// <summary>Backs the Library page (spec section 7.5): browse, search, and manage downloaded media.</summary>
public partial class LibraryViewModel : ObservableObject
{
    public LibraryViewModel()
    {
        LoadSampleData();
    }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _sortBy = "Date Downloaded (Newest)";
    [ObservableProperty] private LibraryMediaFilter _selectedFilter = LibraryMediaFilter.Video;
    [ObservableProperty] private LibraryViewMode _viewMode = LibraryViewMode.Grid;

    public ObservableCollection<LibraryItem> RecentDownloads { get; } = new();
    public ObservableCollection<LibraryGroupSummary> Playlists { get; } = new();
    public ObservableCollection<LibraryGroupSummary> Channels { get; } = new();

    [ObservableProperty]
    private LibraryItem? _selectedItem;

    [RelayCommand]
    private void SelectMediaFilter(LibraryMediaFilter filter) => SelectedFilter = filter;

    [RelayCommand]
    private void SetViewMode(LibraryViewMode mode) => ViewMode = mode;

    [RelayCommand]
    private void SelectItem(LibraryItem item) => SelectedItem = item;

    [RelayCommand] private void Play() { }
    [RelayCommand] private void OpenFile() { }
    [RelayCommand] private void RevealInFolder() { }
    [RelayCommand] private void CopySourceUrl() { }
    [RelayCommand] private void ConvertVideo() { }
    [RelayCommand] private void RemoveFromLibrary() { if (SelectedItem is not null) RecentDownloads.Remove(SelectedItem); }

    private void LoadSampleData()
    {
        var first = new LibraryItem
        {
            Title = "Switzerland 4K \u2013 Scenic Relaxation Film", Channel = "Scenic Relaxation", DurationText = "12:47",
            Format = "MP4", Quality = "1080p", DownloadDateText = "May 22, 2025", IsSelected = true,
            FileSizeText = "1.72 GB", FilePath = @"C:\Users\Alex\Videos\StreamVault\Switzerland 4K – Scenic Relaxation Film.mp4",
            ResolutionText = "1920 x 1080 (16:9)", SourceUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
        };
        first.Tags.Add("relaxation");
        first.Tags.Add("nature");
        first.Tags.Add("switzerland");
        RecentDownloads.Add(first);
        RecentDownloads.Add(new LibraryItem { Title = "The Alps in 8K HDR", Channel = "Scenic Relaxation", DurationText = "04:37", Format = "MP4", Quality = "8K", DownloadDateText = "May 21, 2025" });
        RecentDownloads.Add(new LibraryItem { Title = "Night City Timelapse", Channel = "Urban Explorer", DurationText = "05:46", Format = "MP4", Quality = "4K", DownloadDateText = "May 21, 2025" });
        RecentDownloads.Add(new LibraryItem { Title = "Ocean Waves \u2013 4K", Channel = "Nature Sounds", DurationText = "1:00:00", Format = "MP4", Quality = "4K", DownloadDateText = "May 20, 2025" });
        RecentDownloads.Add(new LibraryItem { Title = "Documentary: The Future of Wildlife", Channel = "Science Hub", DurationText = "0:45:30", Format = "MP4", Quality = "1080p", DownloadDateText = "May 19, 2025" });

        SelectedItem = first;

        Playlists.Add(new LibraryGroupSummary { Name = "Relaxation & Nature", ItemCountText = "12 videos", UpdatedText = "Updated May 22, 2025", IconKey = "IconLayers" });
        Playlists.Add(new LibraryGroupSummary { Name = "City & Urban", ItemCountText = "8 videos", UpdatedText = "Updated May 20, 2025", IconKey = "IconLayers" });
        Playlists.Add(new LibraryGroupSummary { Name = "Documentaries", ItemCountText = "15 videos", UpdatedText = "Updated May 19, 2025", IconKey = "IconLayers" });

        Channels.Add(new LibraryGroupSummary { Name = "Scenic Relaxation", ItemCountText = "24 videos", UpdatedText = "Updated May 22, 2025", IconKey = "IconUser" });
        Channels.Add(new LibraryGroupSummary { Name = "Nature Sounds", ItemCountText = "18 videos", UpdatedText = "Updated May 20, 2025", IconKey = "IconUser" });
        Channels.Add(new LibraryGroupSummary { Name = "Urban Explorer", ItemCountText = "16 videos", UpdatedText = "Updated May 20, 2025", IconKey = "IconUser" });
        Channels.Add(new LibraryGroupSummary { Name = "Science Hub", ItemCountText = "31 videos", UpdatedText = "Updated May 19, 2025", IconKey = "IconUser" });
    }
}
