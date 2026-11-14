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
        var completed = new List<QueueJob>();
        var failed = new List<QueueJob>();
        foreach (var job in _downloads.Jobs.Where(IsTerminal))
        {
            if (!_notified.Add(job)) continue;
            if (_settings.Current.HidePrivateVideos && Yoink.Domain.Media.SourcePrivacy.IsPrivateSource(job.SourceUrl)) continue;
            if (job.Status == QueueJobStatus.Completed) completed.Add(job);
            else failed.Add(job);
        }
        if (completed.Count > 0 && _settings.Current.SoundNotifications) SystemSounds.Asterisk.Play();
        if (_settings.Current.DesktopNotifications && (completed.Count > 0 || failed.Count > 0))
        {
            // Windows only displays one tray balloon at a time. Summarize a batch instead of
            // replacing each notification with the next job in the same queue update.
            var title = failed.Count > 0
                ? failed.Count == 1 ? "Download failed" : $"{failed.Count} downloads failed"
                : completed.Count == 1 ? "Download completed" : $"{completed.Count} downloads completed";
            var detail = failed.Count == 1 ? ShortTitle(failed[0].Title)
                : completed.Count == 1 && failed.Count == 0 ? ShortTitle(completed[0].Title)
                : $"{completed.Count} completed, {failed.Count} failed. Open Yoink for details.";
            _icon.ShowBalloonTip(5000, title, detail,
                failed.Count > 0 ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);
        }
        UpdateBackgroundState();
    }

    private static string ShortTitle(string title) => string.IsNullOrWhiteSpace(title)
        ? "Open Yoink for details."
        : title.Length <= 90 ? title : title[..87] + "...";

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
