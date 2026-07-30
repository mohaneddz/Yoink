using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StreamVault.Presentation.ViewModels.Models;

/// <summary>A row in the Queue page's job table (spec section 7.2), and the source for its inspector detail.</summary>
public partial class QueueJob : ObservableObject
{
    [ObservableProperty] private int _order;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _channel = string.Empty;
    [ObservableProperty] private string _format = "MP4";
    [ObservableProperty] private string _quality = string.Empty;
    [ObservableProperty] private string _sizeText = string.Empty;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _speedText = string.Empty;
    [ObservableProperty] private string _etaText = string.Empty;
    [ObservableProperty] private string _destination = string.Empty;
    [ObservableProperty] private QueueJobStatus _status;
    [ObservableProperty] private string _thumbnailHex = "#FF2B3542";

    // Inspector detail fields
    [ObservableProperty] private string _sourceUrl = string.Empty;
    [ObservableProperty] private string _selectedFormatText = string.Empty;
    [ObservableProperty] private string _outputFolder = string.Empty;
    [ObservableProperty] private string _fileSizeText = string.Empty;
    [ObservableProperty] private string _downloadedText = string.Empty;
    [ObservableProperty] private int _retryCount;
    [ObservableProperty] private string _addedAtText = string.Empty;
    [ObservableProperty] private string _lastUpdateText = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _statusDetailText = string.Empty;
    [ObservableProperty] private string _selectedInspectorTab = "Details";

    public ObservableCollection<string> LogLines { get; } = new();

    /// <summary>Injected by the coordinator so inspector actions route through the real queue engine.</summary>
    public Action? ResumeAction { get; set; }
    public Action? OpenFolderAction { get; set; }

    [RelayCommand]
    private void Resume() => ResumeAction?.Invoke();

    [RelayCommand]
    private void OpenFolder() => OpenFolderAction?.Invoke();

    [RelayCommand]
    private void SelectTab(string tab) => SelectedInspectorTab = tab;
}
