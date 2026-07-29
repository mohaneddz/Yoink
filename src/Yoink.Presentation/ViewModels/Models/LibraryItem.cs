using CommunityToolkit.Mvvm.ComponentModel;

namespace Yoink.Presentation.ViewModels.Models;

/// <summary>A downloaded media card/inspector subject on the Library page (spec section 7.5).</summary>
public partial class LibraryItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int _entryId;
    [ObservableProperty] private Yoink.Shared.DownloadKind _kind;
    [ObservableProperty] private string _thumbnailHex = "#FF2B3542";
    [ObservableProperty] private string _durationText = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _channel = string.Empty;
    [ObservableProperty] private bool _channelVerified = true;
    [ObservableProperty] private string _format = "MP4";
    [ObservableProperty] private string _quality = "1080p";
    [ObservableProperty] private string _audioCodec = "AAC";
    [ObservableProperty] private string _downloadDateText = string.Empty;
    [ObservableProperty] private string _fileSizeText = string.Empty;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private string _videoCodec = "H.264 (AVC)";
    [ObservableProperty] private string _resolutionText = string.Empty;
    [ObservableProperty] private string _frameRateText = "30 FPS";
    [ObservableProperty] private string _sourceUrl = string.Empty;
    [ObservableProperty] private bool _isMissing;
    public System.Collections.ObjectModel.ObservableCollection<string> Tags { get; } = new();
}

public sealed class LibraryGroupSummary
{
    public required string Name { get; init; }
    public required string ItemCountText { get; init; }
    public required string UpdatedText { get; init; }
    public string IconKey { get; init; } = "IconFolder";
}
