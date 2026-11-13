using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yoink.Application.Abstractions;
using Yoink.Presentation.Extensions;
using Yoink.Presentation.Services;
using Yoink.Presentation.ViewModels.Models;

namespace Yoink.Presentation.ViewModels;

/// <summary>Backs the Queue page: monitor, control, and prioritize jobs owned by the coordinator.</summary>
public partial class QueueViewModel : ObservableObject
{
    private readonly InspectorViewModel _inspector;
    private readonly DownloadCoordinator _coordinator;
    private readonly ISettingsService _settings;
    private readonly ICollectionView _filteredJobs;
    private readonly DispatcherTimer _refreshTimer;

    public QueueViewModel(InspectorViewModel inspector, DownloadCoordinator coordinator, ISettingsService settings)
    {
        _inspector = inspector;
        _coordinator = coordinator;
        _settings = settings;
        _maxConcurrent = settings.Current.SimultaneousDownloads;

        _filteredJobs = CollectionViewSource.GetDefaultView(coordinator.Jobs);
        _filteredJobs.Filter = FilterJob;

        coordinator.Jobs.CollectionChanged += OnJobsCollectionChanged;
        coordinator.JobsChanged += (_, _) => RefreshCounts();
        foreach (var job in coordinator.Jobs)
        {
            job.PropertyChanged += OnJobPropertyChanged;
        }

        // Poll the queue so live jobs and the on-disk state of finished ones stay in sync even when
        // no progress events are firing (e.g. a completed file gets deleted outside the app).
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _refreshTimer.Tick += (_, _) =>
        {
            _coordinator.VerifyFilesExist();
            RefreshCounts();
        };
        _refreshTimer.Start();
    }

    public ObservableCollection<QueueJob> Jobs => _coordinator.Jobs;

    public ICollectionView FilteredJobs => _filteredJobs;

    [ObservableProperty] private QueueFilter _selectedFilter = QueueFilter.Queued;
    [ObservableProperty] private int _maxConcurrent;
    [ObservableProperty] private QueueJob? _selectedJob;

    public ObservableCollection<int> ConcurrencyOptions { get; } = new() { 1, 2, 3, 4, 5, 6, 8 };

    public int ActiveCount => Jobs.Count(IsActive);
    /// <summary>All unfinished work, including downloads currently in progress.</summary>
    public int QueuedCount => Jobs.Count(j => IsActive(j) || j.Status is QueueJobStatus.Queued or QueueJobStatus.Waiting or QueueJobStatus.Inspecting or QueueJobStatus.Paused);
    public int DoneCount => CompletedCount + FailedCount;
    public int CompletedCount => Jobs.Count(j => j.Status == QueueJobStatus.Completed);
    public int FailedCount => Jobs.Count(j => j.Status is QueueJobStatus.Failed or QueueJobStatus.Cancelled);

    private static bool IsActive(QueueJob j) => j.Status is QueueJobStatus.DownloadingVideo or QueueJobStatus.DownloadingAudio
        or QueueJobStatus.DownloadingSubtitles or QueueJobStatus.Merging or QueueJobStatus.Converting
        or QueueJobStatus.WritingMetadata or QueueJobStatus.Retrying;

    private void OnJobsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (QueueJob job in e.OldItems)
            {
                job.PropertyChanged -= OnJobPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (QueueJob job in e.NewItems)
            {
                job.PropertyChanged += OnJobPropertyChanged;
            }
        }

        RefreshCounts();
    }

    private void OnJobPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueueJob.Status))
        {
            RefreshCounts();
        }
        else if (e.PropertyName == nameof(QueueJob.IsSelected))
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelectedJobs));
        }
    }

    private bool FilterJob(object obj)
    {
        if (obj is not QueueJob job)
        {
            return false;
        }

        return SelectedFilter switch
        {
            QueueFilter.Queued => IsActive(job) || job.Status is QueueJobStatus.Queued or QueueJobStatus.Waiting or QueueJobStatus.Inspecting or QueueJobStatus.Paused,
            QueueFilter.Done => job.Status is QueueJobStatus.Completed or QueueJobStatus.Failed or QueueJobStatus.Cancelled,
            _ => true
        };
    }

    partial void OnSelectedFilterChanged(QueueFilter value) => _filteredJobs.SafeRefresh();

    partial void OnMaxConcurrentChanged(int value)
    {
        if (value <= 0)
        {
            return;
        }

        _settings.Current.SimultaneousDownloads = value;
        _ = _settings.SaveAsync(_settings.Current);
        _coordinator.Refresh();
    }

    [RelayCommand]
    private void SelectFilter(QueueFilter filter) => SelectedFilter = filter;

    partial void OnSelectedJobChanged(QueueJob? value)
    {
        if (value is not null)
        {
            _inspector.Show(value);
        }
    }

    [ObservableProperty] private bool _isAllSelected;

    public int SelectedCount => Jobs.Count(j => j.IsSelected);
    public bool HasSelectedJobs => SelectedCount > 0;

    partial void OnIsAllSelectedChanged(bool value)
    {
        foreach (var job in _filteredJobs.OfType<QueueJob>())
        {
            job.IsSelected = value;
        }
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelectedJobs));
    }

    [RelayCommand] private void StartAll() => _coordinator.StartAll();
    [RelayCommand] private void PauseAll() => _coordinator.PauseAll();
    [RelayCommand] private void ClearCompleted() => _coordinator.ClearCompleted();
    [RelayCommand] private void ClearFailed() => _coordinator.ClearFailed();
    [RelayCommand] private void RetryFailed() => _coordinator.RetryFailed();
    [RelayCommand] private void ToggleJob(QueueJob job) => _coordinator.ToggleJob(job);
    [RelayCommand] private void PauseJob(QueueJob job) => _coordinator.PauseJob(job);
    [RelayCommand] private void ResumeJob(QueueJob job) => _coordinator.ResumeJob(job);
    [RelayCommand] private void RetryJob(QueueJob job) => _coordinator.RetryJob(job);
    [RelayCommand] private void RemoveJob(QueueJob job) => _coordinator.RemoveJob(job);

    [RelayCommand]
    private void DeleteSelected()
    {
        var selected = Jobs.Where(j => j.IsSelected).ToList();
        _coordinator.RemoveJobs(selected);
        IsAllSelected = false;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelectedJobs));
    }

    [RelayCommand]
    private void PauseSelected()
    {
        var selected = Jobs.Where(j => j.IsSelected).ToList();
        _coordinator.PauseJobs(selected);
    }

    [RelayCommand]
    private void ResumeSelectedBatch()
    {
        var selected = Jobs.Where(j => j.IsSelected).ToList();
        _coordinator.ResumeJobs(selected);
    }

    [RelayCommand]
    private void ResumeSelected()
    {
        if (SelectedJob is not null)
        {
            _coordinator.ResumeJob(SelectedJob);
        }
    }

    private void RefreshCounts()
    {
        OnPropertyChanged(nameof(ActiveCount));
        OnPropertyChanged(nameof(QueuedCount));
        OnPropertyChanged(nameof(DoneCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(FailedCount));
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelectedJobs));
        _filteredJobs.SafeRefresh();
    }
}
