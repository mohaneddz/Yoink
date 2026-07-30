using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVault.Application.Abstractions;
using StreamVault.Presentation.Navigation;
using StreamVault.Presentation.Services;

namespace StreamVault.Presentation.ViewModels;

/// <summary>
/// Top-level view model for the application shell: title bar, sidebar, main surface, and status strip.
/// </summary>
public partial class ShellViewModel : ObservableObject, INavigationService
{
    void INavigationService.Navigate(AppPage page) => NavigateTo(page);

    private readonly Func<AppPage, object> _pageResolver;
    private readonly Dictionary<AppPage, object> _pageCache = new();
    private readonly DownloadCoordinator _coordinator;
    private readonly ISettingsService _settings;
    private readonly DispatcherTimer _statusTimer;

    public ShellViewModel(
        Func<AppPage, object> pageResolver,
        SystemStatusViewModel systemStatus,
        StatusStripViewModel statusStrip,
        InspectorViewModel inspector,
        DownloadCoordinator coordinator,
        ISettingsService settings)
    {
        _pageResolver = pageResolver;
        SystemStatus = systemStatus;
        StatusStrip = statusStrip;
        Inspector = inspector;
        _coordinator = coordinator;
        _settings = settings;

        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _statusTimer.Tick += (_, _) => UpdateStatusStrip();
        _statusTimer.Start();
        UpdateStatusStrip();
    }

    /// <summary>
    /// Selects the initial page. Called after construction so page view models (which depend on
    /// <see cref="INavigationService"/>, i.e. this shell) don't create a construction-time cycle.
    /// </summary>
    public void Initialize() => NavigateTo(AppPage.Download);

    [ObservableProperty] private AppPage _currentPage;
    [ObservableProperty] private object? _currentPageViewModel;

    public SystemStatusViewModel SystemStatus { get; }
    public StatusStripViewModel StatusStrip { get; }
    public InspectorViewModel Inspector { get; }

    [RelayCommand]
    private void NavigateTo(AppPage page)
    {
        if (!_pageCache.TryGetValue(page, out var vm))
        {
            vm = _pageResolver(page);
            _pageCache[page] = vm;
        }

        // Close the shared inspector when switching pages (unless the user pinned it).
        if (!Inspector.IsPinned && page != CurrentPage)
        {
            Inspector.IsOpen = false;
            Inspector.Content = null;
        }

        CurrentPage = page;
        CurrentPageViewModel = vm;
    }

    private void UpdateStatusStrip()
    {
        StatusStrip.ActiveDownloads = _coordinator.ActiveJobCount;
        StatusStrip.FilesInQueue = _coordinator.PendingJobCount;
        StatusStrip.TotalSpeedMbps = _coordinator.AggregateBytesPerSecond / 1_048_576.0;
        StatusStrip.StorageFreeTb = GetFreeSpaceTb(_settings.Current.DefaultDownloadFolder);
    }

    private static double GetFreeSpaceTb(string folder)
    {
        try
        {
            var root = Path.GetPathRoot(folder);
            if (string.IsNullOrEmpty(root))
            {
                return 0;
            }

            var drive = new DriveInfo(root);
            return drive.IsReady ? drive.AvailableFreeSpace / 1_099_511_627_776.0 : 0;
        }
        catch
        {
            return 0;
        }
    }
}
