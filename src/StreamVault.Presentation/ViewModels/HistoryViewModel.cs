using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVault.Presentation.ViewModels.Models;

namespace StreamVault.Presentation.ViewModels;

/// <summary>Backs the History page (spec section 7.6): a complete ledger of download attempts.</summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly InspectorViewModel _inspector;

    public HistoryViewModel(InspectorViewModel inspector)
    {
        _inspector = inspector;
        LoadSampleData();
    }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _dateRangeText = "May 15, 2025 \u2014 May 22, 2025";
    [ObservableProperty] private string _resultFilter = "All Results";
    [ObservableProperty] private string _sourceTypeFilter = "All Sources";
    [ObservableProperty] private string _formatFilter = "All Formats";

    public ObservableCollection<HistoryRecord> Records { get; } = new();

    [ObservableProperty]
    private HistoryRecord? _selectedRecord;

    partial void OnSelectedRecordChanged(HistoryRecord? value)
    {
        if (value is not null)
        {
            _inspector.Show(value);
        }
    }

    [RelayCommand] private void RetrySelected() { }
    [RelayCommand] private void ReuseSettings() { }
    [RelayCommand] private void OpenFolder() { }
    [RelayCommand] private void CopySourceUrl() { }
    [RelayCommand] private void ExportLogs() { }
    [RelayCommand] private void ExportHistory() { }
    [RelayCommand] private void ClearFilters() { ResultFilter = "All Results"; SourceTypeFilter = "All Sources"; FormatFilter = "All Formats"; SearchText = string.Empty; }
    [RelayCommand] private void ClearHistory() { Records.Clear(); }

    private void LoadSampleData()
    {
        (string Title, string Channel, string Timestamp, HistoryResult Result, string Output, string Size, string Duration, string Extra)[] rows =
        {
            ("Switzerland 4K \u2013 Scenic Relaxation Film", "Scenic Relaxation", "May 22, 2025 14:27:31", HistoryResult.Success, @"C:\Videos\Travel", "1.72 GB", "12:47", ""),
            ("The Alps in 8K HDR", "Scenic Relaxation", "May 22, 2025 11:12:45", HistoryResult.Success, @"C:\Videos\8K", "853 MB", "04:37", ""),
            ("Ocean Waves \u2013 4K", "Scenic Relaxation", "May 22, 2025 09:58:12", HistoryResult.Success, @"C:\Videos\Nature", "476 MB", "04:02", ""),
            ("Night City Timelapse", "City Lights", "May 22, 2025 08:42:09", HistoryResult.Success, @"C:\Videos\Timelapse", "281 MB", "03:15", ""),
            ("Documentary: The Future of Wildlife", "Future Docs", "May 21, 2025 21:16:38", HistoryResult.Success, @"C:\Videos\Docs", "1.23 GB", "45:30", ""),
            ("Aurora Borealis \u2013 4K", "Nature Escapes", "May 21, 2025 19:03:21", HistoryResult.Failed, "\u2014", "0 B", "\u2014", ""),
            ("Japan in Spring \u2013 4K", "Scenic Relaxation", "May 21, 2025 17:45:11", HistoryResult.Cancelled, "\u2014", "0 B", "\u2014", ""),
            ("Rainy Day Coffee Shop", "Cozy Ambience", "May 20, 2025 22:33:07", HistoryResult.Success, @"C:\Videos\Ambience", "312 MB", "03:01", ""),
            ("Neon Tokyo \u2013 Night Drive", "City Lights", "May 20, 2025 20:11:55", HistoryResult.Queued, "\u2014", "0 B", "\u2014", ""),
            ("Iceland Waterfalls \u2013 8K", "Scenic Relaxation", "May 20, 2025 18:50:44", HistoryResult.Success, @"C:\Videos\8K", "2.18 GB", "08:23", ""),
            ("Switzerland 4K \u2013 Scenic Relaxation Film", "Scenic Relaxation", "May 20, 2025 14:05:22", HistoryResult.Success, @"C:\Videos\Travel", "1.72 GB", "12:47", "redownloaded"),
            ("City Skyline Drone \u2013 4K", "City Lights", "May 19, 2025 23:58:10", HistoryResult.Failed, "\u2014", "0 B", "\u2014", ""),
        };

        for (var i = 0; i < rows.Length; i++)
        {
            var r = rows[i];
            Records.Add(new HistoryRecord
            {
                Index = i + 1, Title = r.Title, Channel = r.Channel, TimestampText = r.Timestamp, Result = r.Result,
                Output = r.Output, FileSizeText = r.Size, DurationText = r.Duration, Redownloaded = r.Extra == "redownloaded",
                SourceUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ", ResolutionText = "1440p 2K", VideoCodecText = "MP4 (H.264)",
                AudioCodecText = "AAC 192kbps", CompletedText = r.Timestamp, ExitCodeText = r.Result == HistoryResult.Success ? "0 (OK)" : "1 (Error)"
            });
        }

        Records[1].LogLines.Add("11:12:42  [info] Starting download...");
        Records[1].LogLines.Add("11:12:42  [info] Selected stream: 1440p (H.264) + AAC 192k");
        Records[1].LogLines.Add("11:12:42  [info] Destination: C:\\Videos\\8K\\The Alps in 8K HDR.mp4");
        Records[1].LogLines.Add("11:12:43  [info] Downloading metadata...");
        Records[1].LogLines.Add("11:12:45  [info] Writing video stream...");
        Records[1].LogLines.Add("11:16:58  [info] Merging streams...");
        Records[1].LogLines.Add("11:16:59  [info] Download completed successfully.");
        Records[1].LogLines.Add("11:16:59  [info] File saved (853 MB)");

        SelectedRecord = Records[1];
    }
}
