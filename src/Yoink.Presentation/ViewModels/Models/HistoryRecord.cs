using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Yoink.Presentation.ViewModels.Models;

/// <summary>A row in the History page's ledger (spec section 7.6).</summary>
public partial class HistoryRecord : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int _entryId;
    [ObservableProperty] private int _index;
    [ObservableProperty] private string _thumbnailHex = "#FF2B3542";
    [ObservableProperty] private string _thumbnailUrl = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _channel = string.Empty;
    [ObservableProperty] private string _timestampText = string.Empty;
    [ObservableProperty] private DateTimeOffset _timestamp;
    [ObservableProperty] private HistoryResult _result;
    [ObservableProperty] private string _output = string.Empty;
    [ObservableProperty] private string _filePath = string.Empty;
    [ObservableProperty] private Yoink.Shared.DownloadKind _kind;
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

    /// <summary>Injected by the History view model so the inspector's actions reuse its logic.</summary>
    public Action? RetryAction { get; set; }
    public Action? CopyUrlAction { get; set; }
    public Action? OpenFolderAction { get; set; }

    [RelayCommand] private void Retry() => RetryAction?.Invoke();
    [RelayCommand] private void CopyUrl() => CopyUrlAction?.Invoke();
    [RelayCommand] private void OpenFolder() => OpenFolderAction?.Invoke();

    [RelayCommand]
    private void CopyLog()
    {
        if (LogLines.Count > 0)
        {
            try { System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, LogLines)); } catch { }
        }
    }

    [RelayCommand]
    private void ClearLog() => LogLines.Clear();
}
