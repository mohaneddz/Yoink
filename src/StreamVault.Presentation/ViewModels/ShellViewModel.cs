using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVault.Presentation.Navigation;

namespace StreamVault.Presentation.ViewModels;

/// <summary>
/// Top-level view model for the application shell: the four persistent regions (title bar,
/// sidebar, main surface, status strip) described in spec section 4.5.
/// </summary>
public partial class ShellViewModel : ObservableObject, INavigationService
{
    void INavigationService.Navigate(AppPage page) => NavigateTo(page);

    private readonly Func<AppPage, object> _pageResolver;
    private readonly Dictionary<AppPage, object> _pageCache = new();

    public ShellViewModel(Func<AppPage, object> pageResolver, SystemStatusViewModel systemStatus, StatusStripViewModel statusStrip, InspectorViewModel inspector)
    {
        _pageResolver = pageResolver;
        SystemStatus = systemStatus;
        StatusStrip = statusStrip;
        Inspector = inspector;
        NavigateTo(AppPage.Download);
    }

    [ObservableProperty]
    private AppPage _currentPage;

    [ObservableProperty]
    private object? _currentPageViewModel;

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

        CurrentPage = page;
        CurrentPageViewModel = vm;
    }
}
