using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVault.Presentation.ViewModels.Models;

namespace StreamVault.Presentation.ViewModels;

/// <summary>Backs the Queue page (spec section 7.2): monitor, control, and prioritize active jobs.</summary>
public partial class QueueViewModel : ObservableObject
{
    private readonly InspectorViewModel _inspector;
    private readonly ICollectionView _filteredJobs;

    public QueueViewModel(InspectorViewModel inspector)
    {
        _inspector = inspector;
        LoadSampleJobs();

        _filteredJobs = CollectionViewSource.GetDefaultView(Jobs);
        _filteredJobs.Filter = FilterJob;
    }

    public ObservableCollection<QueueJob> Jobs { get; } = new();

    public ICollectionView FilteredJobs => _filteredJobs;

    [ObservableProperty]
    private QueueFilter _selectedFilter = QueueFilter.Active;

    [ObservableProperty]
    private int _maxConcurrent = 3;

    [ObservableProperty]
    private QueueJob? _selectedJob;

    public int ActiveCount => Jobs.Count(IsActive);
    public int QueuedCount => Jobs.Count(j => j.Status is QueueJobStatus.Queued or QueueJobStatus.Waiting or QueueJobStatus.Inspecting);
    public int PausedCount => Jobs.Count(j => j.Status == QueueJobStatus.Paused);
    public int CompletedCount => Jobs.Count(j => j.Status == QueueJobStatus.Completed);
    public int FailedCount => Jobs.Count(j => j.Status is QueueJobStatus.Failed or QueueJobStatus.Cancelled);

    private static bool IsActive(QueueJob j) => j.Status is QueueJobStatus.DownloadingVideo or QueueJobStatus.DownloadingAudio
        or QueueJobStatus.DownloadingSubtitles or QueueJobStatus.Merging or QueueJobStatus.Converting
        or QueueJobStatus.WritingMetadata or QueueJobStatus.Retrying;

    private bool FilterJob(object obj)
    {
        if (obj is not QueueJob job)
        {
            return false;
        }

        return SelectedFilter switch
        {
            QueueFilter.Active => IsActive(job),
            QueueFilter.Queued => job.Status is QueueJobStatus.Queued or QueueJobStatus.Waiting or QueueJobStatus.Inspecting,
            QueueFilter.Paused => job.Status == QueueJobStatus.Paused,
            QueueFilter.Completed => job.Status == QueueJobStatus.Completed,
            QueueFilter.Failed => job.Status is QueueJobStatus.Failed or QueueJobStatus.Cancelled,
            _ => true
        };
    }

    [RelayCommand]
    private void SelectFilter(QueueFilter filter)
    {
        SelectedFilter = filter;
        _filteredJobs.Refresh();
    }

    partial void OnSelectedJobChanged(QueueJob? value)
    {
        if (value is not null)
        {
            _inspector.Show(value);
        }
    }

    [RelayCommand] private void StartAll() { foreach (var j in Jobs.Where(j => j.Status is QueueJobStatus.Queued or QueueJobStatus.Paused)) j.Status = QueueJobStatus.DownloadingVideo; RefreshCounts(); }
    [RelayCommand] private void PauseAll() { foreach (var j in Jobs.Where(IsActive)) j.Status = QueueJobStatus.Paused; RefreshCounts(); }
    [RelayCommand] private void ClearCompleted() { foreach (var j in Jobs.Where(j => j.Status == QueueJobStatus.Completed).ToList()) Jobs.Remove(j); RefreshCounts(); }
    [RelayCommand] private void RetryFailed() { foreach (var j in Jobs.Where(j => j.Status == QueueJobStatus.Failed)) j.Status = QueueJobStatus.Retrying; RefreshCounts(); }

    [RelayCommand]
    private void ToggleJob(QueueJob job)
    {
        job.Status = IsActive(job) ? QueueJobStatus.Paused : job.Status == QueueJobStatus.Failed ? QueueJobStatus.Retrying : QueueJobStatus.DownloadingVideo;
        RefreshCounts();
    }

    [RelayCommand]
    private void ResumeSelected()
    {
        if (SelectedJob is null)
        {
            return;
        }

        SelectedJob.Status = QueueJobStatus.DownloadingVideo;
        SelectedJob.StatusDetailText = "Resumed by user";
        SelectedJob.LogLines.Add($"{DateTime.Now:HH:mm:ss}  Resumed by user");
        RefreshCounts();
    }

    private void RefreshCounts()
    {
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(QueuedCount));
        OnPropertyChanged(nameof(PausedCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(FailedCount));
        _filteredJobs.Refresh();
    }

    private void LoadSampleJobs()
    {
        Jobs.Add(new QueueJob { Order = 1, Title = "Switzerland 4K \u2013 Scenic Relaxation Film with Calming Music", Channel = "Scenic Relaxation", Quality = "2160p (4K)", SizeText = "1.72 GB", Progress = 73, SpeedText = "18.7 MB/s", EtaText = "02:14", Destination = @"C:\Videos\Travel", Status = QueueJobStatus.DownloadingVideo, SourceUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ", SelectedFormatText = "2160p (4K) MP4 (H.264 / AAC 320k)", OutputFolder = @"C:\Videos\Travel", FileSizeText = "1.72 GB", DownloadedText = "1.26 GB (73%)", AddedAtText = "May 22, 2025 14:24:10", LastUpdateText = "May 22, 2025 14:29:03" });
        Jobs.Add(new QueueJob { Order = 2, Title = "The Alps in 8K HDR", Channel = "Scenic Relaxation", Quality = "1440p (2K)", SizeText = "853 MB", Progress = 41, SpeedText = "12.3 MB/s", EtaText = "04:37", Destination = @"C:\Videos\8K", Status = QueueJobStatus.DownloadingVideo, SourceUrl = "https://www.youtube.com/watch?v=alps8k", SelectedFormatText = "1440p (2K) MP4 (H.264 / AAC 192k)", OutputFolder = @"C:\Videos\8K", FileSizeText = "853 MB", DownloadedText = "350 MB (41%)", AddedAtText = "May 22, 2025 14:20:02", LastUpdateText = "May 22, 2025 14:29:01" });
        Jobs.Add(new QueueJob { Order = 3, Title = "Night City Timelapse", Channel = "Timelapse World", Quality = "1080p (FHD)", SizeText = "476 MB", Progress = 0, SpeedText = "\u2014", EtaText = "\u2014", Destination = @"C:\Videos\Timelapse", Status = QueueJobStatus.Queued, SourceUrl = "https://www.youtube.com/watch?v=nightcity", SelectedFormatText = "1080p (FHD) MP4 (H.264 / AAC 192k)", OutputFolder = @"C:\Videos\Timelapse", FileSizeText = "476 MB", DownloadedText = "0 B (0%)", AddedAtText = "May 22, 2025 14:24:11" });
        Jobs.Add(new QueueJob { Order = 4, Title = "Ocean Waves \u2013 4K", Channel = "Nature Relaxation", Quality = "720p (HD)", SizeText = "281 MB", Progress = 0, SpeedText = "\u2014", EtaText = "\u2014", Destination = @"C:\Videos\Nature", Status = QueueJobStatus.Queued, SourceUrl = "https://www.youtube.com/watch?v=oceanwaves", SelectedFormatText = "720p (HD) MP4 (H.264 / AAC 128k)", OutputFolder = @"C:\Videos\Nature", FileSizeText = "281 MB", DownloadedText = "0 B (0%)", AddedAtText = "May 22, 2025 14:24:12" });
        Jobs.Add(new QueueJob
        {
            Order = 5, Title = "Documentary: The Future of Wildlife", Channel = "Nature Docs", Quality = "480p (SD)", SizeText = "153 MB",
            Progress = 0, SpeedText = "\u2014", EtaText = "\u2014", Destination = @"C:\Videos\Docs", Status = QueueJobStatus.Paused,
            SourceUrl = "https://www.youtube.com/watch?v=dQw4w9WgXc-wildlife", SelectedFormatText = "480p (SD) MP4 (H.264 / AAC 96k)",
            OutputFolder = @"C:\Videos\Docs", FileSizeText = "153 MB", DownloadedText = "0 B (0%)", StatusDetailText = "Paused by user",
            AddedAtText = "May 22, 2025 14:24:10", LastUpdateText = "May 22, 2025 14:29:03"
        });
        Jobs[4].LogLines.Add("14:24:10  Added to queue");
        Jobs[4].LogLines.Add("14:24:11  Validating URL");
        Jobs[4].LogLines.Add("14:24:11  URL is valid");
        Jobs[4].LogLines.Add("14:24:12  Fetching stream info");
        Jobs[4].LogLines.Add("14:24:12  Stream info retrieved");
        Jobs[4].LogLines.Add("14:24:13  Selected format: 480p (MP4)");
        Jobs[4].LogLines.Add("14:24:13  Output: C:\\Videos\\Docs");
        Jobs[4].LogLines.Add("14:24:14  Paused by user");

        Jobs.Add(new QueueJob { Order = 6, Title = "Waterfalls of the World \u2013 4K", Channel = "Scenic Relaxation", Quality = "1080p (FHD)", SizeText = "512 MB", Progress = 100, SpeedText = "\u2014", EtaText = "\u2014", Destination = @"C:\Videos\Travel", Status = QueueJobStatus.Completed, SourceUrl = "https://www.youtube.com/watch?v=waterfalls", SelectedFormatText = "1080p (FHD) MP4 (H.264 / AAC 192k)", OutputFolder = @"C:\Videos\Travel", FileSizeText = "512 MB", DownloadedText = "512 MB (100%)", AddedAtText = "May 22, 2025 13:50:00", LastUpdateText = "May 22, 2025 14:02:00" });
        Jobs.Add(new QueueJob { Order = 7, Title = "Desert Landscapes in 4K", Channel = "Scenic Relaxation", Quality = "1080p (FHD)", SizeText = "627 MB", Progress = 0, SpeedText = "\u2014", EtaText = "\u2014", Destination = @"C:\Videos\Travel", Status = QueueJobStatus.Failed, SourceUrl = "https://www.youtube.com/watch?v=desert4k", SelectedFormatText = "1080p (FHD) MP4 (H.264 / AAC 192k)", OutputFolder = @"C:\Videos\Travel", FileSizeText = "627 MB", DownloadedText = "0 B (0%)", StatusDetailText = "Network error while fetching stream", RetryCount = 2, AddedAtText = "May 22, 2025 13:40:00", LastUpdateText = "May 22, 2025 13:41:12" });
        Jobs.Add(new QueueJob { Order = 8, Title = "Rainforest Ambience with Birdsong", Channel = "Nature Relaxation", Quality = "360p (SD)", SizeText = "98 MB", Progress = 0, SpeedText = "\u2014", EtaText = "\u2014", Destination = @"C:\Videos\Nature", Status = QueueJobStatus.Queued, SourceUrl = "https://www.youtube.com/watch?v=rainforest", SelectedFormatText = "360p (SD) MP4 (H.264 / AAC 96k)", OutputFolder = @"C:\Videos\Nature", FileSizeText = "98 MB", DownloadedText = "0 B (0%)", AddedAtText = "May 22, 2025 14:24:15" });

        SelectedJob = Jobs[4];
    }
}
