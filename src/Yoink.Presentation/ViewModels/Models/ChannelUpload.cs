using CommunityToolkit.Mvvm.ComponentModel;

namespace Yoink.Presentation.ViewModels.Models;

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
    [ObservableProperty] private string _thumbnailUrl = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _dateText = string.Empty;
    [ObservableProperty] private string _durationText = string.Empty;
    [ObservableProperty] private string _viewsText = string.Empty;
    [ObservableProperty] private ChannelUploadType _type;
    [ObservableProperty] private bool _downloaded;
    [ObservableProperty] private bool _isSeen;

    public bool IsNew => !IsSeen && !Downloaded;

    partial void OnIsSeenChanged(bool value) => OnPropertyChanged(nameof(IsNew));
    partial void OnDownloadedChanged(bool value) => OnPropertyChanged(nameof(IsNew));

    public DateOnly? UploadDate { get; set; }
}
