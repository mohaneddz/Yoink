using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Yoink.Application.Abstractions;
using Yoink.Presentation.Services;
using Yoink.Presentation.ViewModels;
using Yoink.Presentation.Views.Dialogs;

namespace Yoink.Presentation;

public partial class MainWindow : Window
{
    private readonly DownloadCoordinator _coordinator;
    private readonly ISettingsService _settingsService;
    private bool _exitRequested;
    private bool _sessionEnding;
    public bool TrayAvailable { get; set; }

    public MainWindow(ShellViewModel shellViewModel, DownloadCoordinator coordinator, ISettingsService settingsService)
    {
        _coordinator = coordinator;
        _settingsService = settingsService;
        InitializeComponent();
        DataContext = shellViewModel;
        RestoreWindowGeometry();
        StateChanged += OnStateChanged;
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
    }

    private void RestoreWindowGeometry()
    {
        var s = _settingsService.Current;
        if (s.WindowWidth >= MinWidth)
        {
            Width = s.WindowWidth;
        }

        if (s.WindowHeight >= MinHeight)
        {
            Height = s.WindowHeight;
        }

        if (s.WindowLeft.HasValue && s.WindowTop.HasValue)
        {
            var virtualLeft = SystemParameters.VirtualScreenLeft;
            var virtualTop = SystemParameters.VirtualScreenTop;
            var virtualWidth = SystemParameters.VirtualScreenWidth;
            var virtualHeight = SystemParameters.VirtualScreenHeight;

            if (s.WindowLeft.Value >= virtualLeft - 100 &&
                s.WindowLeft.Value < virtualLeft + virtualWidth - 100 &&
                s.WindowTop.Value >= virtualTop - 100 &&
                s.WindowTop.Value < virtualTop + virtualHeight - 100)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = s.WindowLeft.Value;
                Top = s.WindowTop.Value;
            }
        }

        if (s.IsWindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SaveWindowGeometry()
    {
        var s = _settingsService.Current;
        if (WindowState == WindowState.Maximized)
        {
            s.IsWindowMaximized = true;
            if (RestoreBounds.Width >= MinWidth && RestoreBounds.Height >= MinHeight)
            {
                s.WindowWidth = RestoreBounds.Width;
                s.WindowHeight = RestoreBounds.Height;
                s.WindowLeft = RestoreBounds.Left;
                s.WindowTop = RestoreBounds.Top;
            }
        }
        else
        {
            s.IsWindowMaximized = false;
            if (Width >= MinWidth && Height >= MinHeight)
            {
                s.WindowWidth = Width;
                s.WindowHeight = Height;
                s.WindowLeft = Left;
                s.WindowTop = Top;
            }
        }

        _ = _settingsService.SaveAsync(s);
    }

    /// <summary>Spec §8.7: confirm before closing over active downloads (they'll be interrupted otherwise).</summary>
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        SaveWindowGeometry();

        if (_sessionEnding) { _coordinator.PauseAll(); return; }
        if (!_exitRequested && TrayAvailable && _settingsService.Current.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        var active = _coordinator.ActiveJobCount;
        if (active == 0)
        {
            return;
        }

        var dialog = new ExitConfirmationDialog(active) { Owner = this };
        dialog.ShowDialog();

        switch (dialog.Choice)
        {
            case ExitChoice.Return:
                e.Cancel = true;
                _exitRequested = false;
                break;
            case ExitChoice.PauseAndExit:
                _coordinator.PauseAll();
                break;
            case ExitChoice.CancelDownloadsAndExit:
                _coordinator.CancelActiveJobs();
                break;
        }
    }

    public void RequestExit()
    {
        _exitRequested = true;
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Close();
    }

    public void PrepareForSessionEnd() => _sessionEnding = true;

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Swap the maximize/restore glyph and tooltip to match the current window state.</summary>
    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && TrayAvailable && _settingsService.Current.MinimizeToTray)
            Hide();
        var maximized = WindowState == WindowState.Maximized;
        if (MaximizeGlyph is Path glyph && TryFindResource(maximized ? "IconRestore" : "IconSquare") is Geometry data)
        {
            glyph.Data = data;
            glyph.Width = maximized ? 14 : 13;
            glyph.Height = maximized ? 14 : 13;
        }

        if (MaximizeButton is not null)
        {
            MaximizeButton.ToolTip = maximized ? "Restore" : "Maximize";
        }
    }

    // ---- Correct maximized bounds so a borderless (WindowChrome) window fills the work
    //      area exactly instead of spilling over the taskbar / screen edges. ----

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
    }

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        const int MONITOR_DEFAULTTONEAREST = 0x00000002;
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(monitor, ref monitorInfo);
            var work = monitorInfo.rcWork;
            var area = monitorInfo.rcMonitor;

            mmi.ptMaxPosition.X = work.Left - area.Left;
            mmi.ptMaxPosition.Y = work.Top - area.Top;
            mmi.ptMaxSize.X = work.Right - work.Left;
            mmi.ptMaxSize.Y = work.Bottom - work.Top;
        }

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }
}
