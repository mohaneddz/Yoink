using CommunityToolkit.Mvvm.ComponentModel;

namespace Yoink.Presentation.ViewModels.Models;

public enum PlaylistEntryAvailability
{
    Available,
    Unavailable,
    Private
}

/// <summary>A row in the Playlists page's entry table (spec section 7.3).</summary>
public partial class PlaylistEntry : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private int _index;
    [ObservableProperty] private string _thumbnailHex = "#FF2B3542";
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _duration = string.Empty;
    [ObservableProperty] private string _channel = string.Empty;
    [ObservableProperty] private PlaylistEntryAvailability _availability = PlaylistEntryAvailability.Available;
    [ObservableProperty] private string _format = "Best (4K)";
    [ObservableProperty] private bool _previouslyDownloaded;
}
