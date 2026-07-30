using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamVault.Presentation.ViewModels.Models;

/// <summary>A row in the History page's ledger (spec section 7.6).</summary>
public partial class HistoryRecord : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int _entryId;
    [ObservableProperty] private int _index;
    [ObservableProperty] private string _thumbnailHex = "#FF2B3542";
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _channel = string.Empty;
    [ObservableProperty] private string _timestampText = string.Empty;
    [ObservableProperty] private HistoryResult _result;
    [ObservableProperty] private string _output = string.Empty;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private StreamVault.Shared.DownloadKind _kind;
    [ObservableProperty] private string _fileSizeText = string.Empty;
    [ObservableProperty] private string _format = "MP4 (H.264)";
    [ObservableProperty] private string _durationText = string.Empty;
    [ObservableProperty] private bool _redownloaded;

    // Inspector detail
    [ObservableProperty] private string _sourceUrl = string.Empty;
    [ObservableProperty] private string _resolutionText = string.Empty;
    [ObservableProperty] private string _videoCodecText = string.Empty;
    [ObservableProperty] private string _audioCodecText = string.Empty;
    [ObservableProperty] private int _fps = 30;
    [ObservableProperty] private string _completedText = string.Empty;
    [ObservableProperty] private string _exitCodeText = "0 (OK)";
    public System.Collections.ObjectModel.ObservableCollection<string> LogLines { get; } = new();
}
