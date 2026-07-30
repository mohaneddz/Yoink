using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamVault.Presentation.ViewModels.Models;

public enum ChannelUploadType
{
    Video,
    Short,
    Live,
    Playlist
}

/// <summary>A row in the selected channel's uploads list (spec section 7.4).</summary>
public partial class ChannelUpload : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _thumbnailHex = "#FF2B3542";
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _dateText = string.Empty;
    [ObservableProperty] private string _durationText = string.Empty;
    [ObservableProperty] private string _viewsText = string.Empty;
    [ObservableProperty] private ChannelUploadType _type;
    [ObservableProperty] private bool _downloaded;
}
