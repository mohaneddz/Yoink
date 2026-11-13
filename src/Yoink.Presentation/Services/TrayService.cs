using System.Drawing;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Yoink.Application.Abstractions;
using Yoink.Presentation.ViewModels.Models;
using Forms = System.Windows.Forms;

namespace Yoink.Presentation.Services;

/// <summary>Background window controls, download notifications, and active-download sleep protection.</summary>
public sealed class TrayService : IDisposable
{
    private readonly MainWindow _window;
    private readonly DownloadCoordinator _downloads;
    private readonly ISettingsService _settings;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Icon _image;
    private readonly DispatcherTimer _timer;
    private readonly HashSet<QueueJob> _notified = new();
    private bool _preventingSleep;

    public TrayService(MainWindow window, DownloadCoordinator downloads, ISettingsService settings)
    {
        _window = window;
        _downloads = downloads;
        _settings = settings;
        using var stream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Yoink;component/app.ico"))!.Stream;
        using var image = new Icon(stream);
        _image = (Icon)image.Clone();
        _menu = new Forms.ContextMenuStrip();
        _menu.Items.Add("Open Yoink", null, (_, _) => OnUi(ShowWindow));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("Pause downloads", null, (_, _) => OnUi(_downloads.PauseAll));
        _menu.Items.Add("Resume paused downloads", null, (_, _) => OnUi(_downloads.ResumePaused));
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add("Exit Yoink", null, (_, _) => OnUi(_window.RequestExit));
        _icon = new Forms.NotifyIcon { Icon = _image, Text = "Yoink", ContextMenuStrip = _menu, Visible = true };
        _icon.DoubleClick += (_, _) => OnUi(ShowWindow);
        _icon.BalloonTipClicked += (_, _) => OnUi(ShowWindow);
        foreach (var job in downloads.Jobs.Where(IsTerminal)) _notified.Add(job);
        _downloads.JobsChanged += OnJobsChanged;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => OnJobsChanged(this, EventArgs.Empty);
        _timer.Start();
        _window.TrayAvailable = true;
    }

    private void OnUi(Action action) => _window.Dispatcher.BeginInvoke(action);

    public void ShowWindow()
    {
        _window.Show();
        if (_window.WindowState == System.Windows.WindowState.Minimized)
            _window.WindowState = _settings.Current.IsWindowMaximized ? System.Windows.WindowState.Maximized : System.Windows.WindowState.Normal;
        _window.Activate();
    }

    private static bool IsTerminal(QueueJob job) => job.Status is QueueJobStatus.Completed or QueueJobStatus.Failed;

    private void OnJobsChanged(object? sender, EventArgs e)
    {
        if (!_window.Dispatcher.CheckAccess()) { OnUi(() => OnJobsChanged(sender, e)); return; }
        _notified.RemoveWhere(job => !_downloads.Jobs.Contains(job) || !IsTerminal(job));
        foreach (var job in _downloads.Jobs.Where(IsTerminal))
        {
            if (!_notified.Add(job)) continue;
            if (_settings.Current.HidePrivateVideos && Yoink.Domain.Media.SourcePrivacy.IsPrivateSource(job.SourceUrl)) continue;
            var success = job.Status == QueueJobStatus.Completed;
            if (_settings.Current.DesktopNotifications)
                _icon.ShowBalloonTip(4000, success ? "Download completed" : "Download failed",
                    success ? "Your file is ready in the download folder." : "Open Yoink to review the download and retry.",
                    success ? Forms.ToolTipIcon.Info : Forms.ToolTipIcon.Warning);
            if (success && _settings.Current.SoundNotifications) SystemSounds.Asterisk.Play();
        }
        UpdateBackgroundState();
    }

    private void UpdateBackgroundState()
    {
        var active = _downloads.ActiveJobCount;
        var visibleActive = _downloads.VisibleActiveJobCount;
        _icon.Text = visibleActive > 0 ? $"Yoink - {visibleActive} active download(s)" : "Yoink";
        var preventSleep = active > 0 && _settings.Current.PreventSleepWhileDownloading;
        if (preventSleep == _preventingSleep) return;
        _preventingSleep = preventSleep;
        SetThreadExecutionState(0x80000000u | (preventSleep ? 0x00000001u : 0));
    }

    public void Dispose()
    {
        _timer.Stop();
        _downloads.JobsChanged -= OnJobsChanged;
        _window.TrayAvailable = false;
        if (_preventingSleep) SetThreadExecutionState(0x80000000u);
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
        _image.Dispose();
    }

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint flags);
}
