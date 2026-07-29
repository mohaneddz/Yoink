using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StreamVault.Presentation.ViewModels;

public enum SystemHealthState
{
    Operational,
    Degraded,
    Offline,
    MissingFFmpeg,
    MissingYtDlp,
    UpdateRequired,
    StorageUnavailable
}

/// <summary>Backs the sidebar's "SYSTEM STATUS" block (spec section 6).</summary>
public partial class SystemStatusViewModel : ObservableObject
{
    [ObservableProperty]
    private SystemHealthState _state = SystemHealthState.Operational;

    [ObservableProperty]
    private string _detail = "All systems normal";

    public string StateLabel => State switch
    {
        SystemHealthState.Operational => "OPERATIONAL",
        SystemHealthState.Degraded => "DEGRADED",
        SystemHealthState.Offline => "OFFLINE",
        SystemHealthState.MissingFFmpeg => "MISSING FFMPEG",
        SystemHealthState.MissingYtDlp => "MISSING YT-DLP",
        SystemHealthState.UpdateRequired => "UPDATE REQUIRED",
        SystemHealthState.StorageUnavailable => "STORAGE UNAVAILABLE",
        _ => "UNKNOWN"
    };

    public Brush DotBrush => State switch
    {
        SystemHealthState.Operational => (Brush)System.Windows.Application.Current.Resources["SuccessBrush"],
        SystemHealthState.Degraded => (Brush)System.Windows.Application.Current.Resources["WarningBrush"],
        _ => (Brush)System.Windows.Application.Current.Resources["ErrorBrush"]
    };

    partial void OnStateChanged(SystemHealthState value)
    {
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(DotBrush));
    }
}
