using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamVault.Presentation.ViewModels.Models;

/// <summary>A saved channel row/detail (spec section 7.4).</summary>
public partial class SavedChannel : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int _entryId;
    [ObservableProperty] private string _channelUrl = string.Empty;
    [ObservableProperty] private string _avatarHex = "#FFFF4F0A";
    [ObservableProperty] private string _initial = "S";
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _handle = string.Empty;
    [ObservableProperty] private bool _verified = true;
    [ObservableProperty] private string _subscriberCountText = string.Empty;
    [ObservableProperty] private string _videoCountText = string.Empty;
    [ObservableProperty] private string _shortCountText = string.Empty;
    [ObservableProperty] private string _liveCountText = string.Empty;
    [ObservableProperty] private string _playlistCountText = string.Empty;
    [ObservableProperty] private string _lastCheckedText = string.Empty;
    [ObservableProperty] private string _nextCheckText = string.Empty;
    [ObservableProperty] private string _checkFrequencyText = "6 hours";
    [ObservableProperty] private string _joinedText = string.Empty;
    [ObservableProperty] private string _countryText = string.Empty;
    [ObservableProperty] private string _languageText = string.Empty;
    [ObservableProperty] private string _totalViewsText = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
}
