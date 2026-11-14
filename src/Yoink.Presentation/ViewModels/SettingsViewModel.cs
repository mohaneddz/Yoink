using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yoink.Application.Abstractions;
using Yoink.Domain.Settings;
using Yoink.Presentation.Services;
using Yoink.Presentation.ViewModels.Models;

namespace Yoink.Presentation.ViewModels;

/// <summary>Backs the Settings page: edit, persist, and reset application configuration.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IToolProvisioner _tools;
    private readonly ISystemAccess _system;
    private readonly DownloadCoordinator _coordinator;
    private readonly IWindowsStartupService _startup;

    public SettingsViewModel(ISettingsService settings, IToolProvisioner tools, ISystemAccess system, DownloadCoordinator coordinator, IWindowsStartupService startup)
    {
        _settings = settings;
        _tools = tools;
        _system = system;
        _coordinator = coordinator;
        _startup = startup;
        LoadFrom(settings.Current);
        _ = InitializeSystemInfoAsync();
    }

    [ObservableProperty] private SettingsSection _selectedSection = SettingsSection.General;

    [RelayCommand]
    private void SelectSection(SettingsSection section) => SelectedSection = section;

    // General
    [ObservableProperty] private string _defaultDownloadFolder = string.Empty;
    [ObservableProperty] private string _simultaneousDownloads = "3";
    [ObservableProperty] private string _downloadSpeedLimit = "0 (Unlimited)";
    [ObservableProperty] private bool _desktopNotifications = true;
    [ObservableProperty] private bool _soundNotifications;
    [ObservableProperty] private bool _closeToTray;
    [ObservableProperty] private bool _minimizeToTray;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _resumeInterruptedDownloads;
    [ObservableProperty] private bool _preventSleepWhileDownloading;
    [ObservableProperty] private bool _reuseRecentInspection;
    [ObservableProperty] private int _concurrentFragments = 8;
    public ObservableCollection<int> FragmentOptions { get; } = new() { 1, 2, 4, 8, 16 };
    [ObservableProperty] private int _inspectionCacheMinutes = 2;
    public ObservableCollection<int> InspectionCacheOptions { get; } = new() { 0, 1, 2, 5, 10 };
    [ObservableProperty] private string _preferredVideoContainer = "MP4";
    [ObservableProperty] private string _preferredAudioContainer = "M4A";
    [ObservableProperty] private string _preferredVideoQuality = "Best Quality";
    [ObservableProperty] private string _preferredAudioQuality = "Best";
    [ObservableProperty] private string _defaultSubtitles = "English";
    [ObservableProperty] private string _filenameTemplate = "%(title)s [%(id)s].%(ext)s";
    [ObservableProperty] private string _proxy = string.Empty;
    [ObservableProperty] private string _cookiesFile = string.Empty;
    [ObservableProperty] private string _ytDlpPath = string.Empty;
    [ObservableProperty] private string _ffmpegPath = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // Option lists
    public ObservableCollection<string> SimultaneousOptions { get; } = new() { "1", "2", "3", "4", "5", "6", "8" };
    public ObservableCollection<string> SpeedLimitOptions { get; } =
        new() { "0 (Unlimited)", "512 KB/s", "1 MB/s", "2 MB/s", "5 MB/s", "10 MB/s", "20 MB/s", "50 MB/s" };
    public ObservableCollection<string> VideoContainerOptions { get; } = new() { "MP4", "MKV", "WEBM" };
    public ObservableCollection<string> AudioContainerOptions { get; } = new() { "M4A", "MP3", "OPUS", "FLAC", "WAV" };
    public ObservableCollection<string> VideoQualityOptions { get; } =
        new() { "Best Quality", "2160p", "1440p", "1080p", "720p", "480p" };
    public ObservableCollection<string> AudioQualityOptions { get; } =
        new() { "Best", "320 kbps", "256 kbps", "192 kbps", "128 kbps" };
    public ObservableCollection<string> SubtitleOptions { get; } = new() { "None", "English", "Spanish", "French", "German", "Japanese" };

    // System info
    [ObservableProperty] private string _appVersion = "1.0.0";
    [ObservableProperty] private string _ytDlpVersion = "checking…";
    [ObservableProperty] private string _ffmpegVersion = "checking…";
    [ObservableProperty] private string _lastChecked = "—";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(UpdateButtonText))] private bool _hasUpdate;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(UpdateButtonText)), NotifyCanExecuteChangedFor(nameof(CheckUpdatesCommand))]
    private bool _isToolsBusy;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(UpdateButtonText))] private bool _isUpdatingTools;
    [ObservableProperty] private string _ytDlpBadgeText = "CHECKING";
    [ObservableProperty] private bool _isYtDlpUpdated = true;
    [ObservableProperty] private bool _isYtDlpAvailable;
    [ObservableProperty] private bool _isFfmpegAvailable;
    [ObservableProperty] private string _ffmpegBadgeText = "CHECKING";
    private int _toolOperationVersion;

    public string UpdateButtonText => IsToolsBusy
        ? IsUpdatingTools ? "UPDATING…" : "CHECKING…"
        : HasUpdate ? "UPDATE NOW" : "CHECK UPDATES";

    // Storage
    [ObservableProperty] private string _downloadsPath = string.Empty;
    [ObservableProperty] private string _tempPath = string.Empty;
    [ObservableProperty] private string _cachePath = string.Empty;

    // Appearance
    [ObservableProperty] private string _theme = "Light";
    [ObservableProperty] private string _density = "Comfortable";

    // Advanced
    [ObservableProperty] private bool _diagnosticLogging;
    [ObservableProperty] private bool _verboseOutput;

    [ObservableProperty] private bool _hidePrivateVideos;

    private void LoadFrom(AppSettings s)
    {
        DefaultDownloadFolder = s.DefaultDownloadFolder;
        SimultaneousDownloads = s.SimultaneousDownloads.ToString();
        DownloadSpeedLimit = KbpsToLabel(s.DownloadSpeedLimitKbps);
        if (!SpeedLimitOptions.Contains(DownloadSpeedLimit)) SpeedLimitOptions.Add(DownloadSpeedLimit);
        DesktopNotifications = s.DesktopNotifications;
        SoundNotifications = s.SoundNotifications;
        CloseToTray = s.CloseToTray;
        MinimizeToTray = s.MinimizeToTray;
        StartWithWindows = s.StartWithWindows;
        StartMinimized = s.StartMinimized;
        ResumeInterruptedDownloads = s.ResumeInterruptedDownloads;
        PreventSleepWhileDownloading = s.PreventSleepWhileDownloading;
        ReuseRecentInspection = s.ReuseRecentInspection;
        HidePrivateVideos = s.HidePrivateVideos;
        ConcurrentFragments = Math.Clamp(s.ConcurrentFragments, 1, 16);
        InspectionCacheMinutes = Math.Clamp(s.InspectionCacheMinutes, 0, 10);
        PreferredVideoContainer = s.PreferredVideoContainer.ToUpperInvariant();
        PreferredAudioContainer = s.PreferredAudioContainer.ToUpperInvariant();
        PreferredVideoQuality = s.PreferredVideoQuality == "best" ? "Best Quality" : s.PreferredVideoQuality;
        PreferredAudioQuality = s.PreferredAudioQuality == "best" ? "Best" : s.PreferredAudioQuality;
        DefaultSubtitles = LangToLabel(s.DefaultSubtitleLanguage);
        FilenameTemplate = s.FilenameTemplate;
        Proxy = s.Proxy;
        CookiesFile = s.CookiesFile;
        YtDlpPath = s.YtDlpPath;
        FfmpegPath = s.FfmpegPath;
        Theme = s.Theme;
        Density = s.Density;
        DiagnosticLogging = s.DiagnosticLogging;
        VerboseOutput = s.VerboseOutput;

        DownloadsPath = s.DefaultDownloadFolder;
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Yoink");
        TempPath = Path.Combine(dataDir, "temp");
        CachePath = Path.Combine(dataDir, "cache");
        AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    }

    private AppSettings BuildSettings()
    {
        var s = _settings.Current;
        s.DefaultDownloadFolder = DefaultDownloadFolder;
        s.SimultaneousDownloads = int.TryParse(SimultaneousDownloads, out var n) ? n : 3;
        s.DownloadSpeedLimitKbps = LabelToKbps(DownloadSpeedLimit);
        s.DesktopNotifications = DesktopNotifications;
        s.SoundNotifications = SoundNotifications;
        s.CloseToTray = CloseToTray;
        s.MinimizeToTray = MinimizeToTray;
        s.StartWithWindows = StartWithWindows;
        s.StartMinimized = StartMinimized;
        s.ResumeInterruptedDownloads = ResumeInterruptedDownloads;
        s.PreventSleepWhileDownloading = PreventSleepWhileDownloading;
        s.ReuseRecentInspection = ReuseRecentInspection;
        s.HidePrivateVideos = HidePrivateVideos;
        s.ConcurrentFragments = Math.Clamp(ConcurrentFragments, 1, 16);
        s.InspectionCacheMinutes = Math.Clamp(InspectionCacheMinutes, 0, 10);
        s.PreferredVideoContainer = PreferredVideoContainer.ToLowerInvariant();
        s.PreferredAudioContainer = PreferredAudioContainer.ToLowerInvariant();
        s.PreferredVideoQuality = PreferredVideoQuality == "Best Quality" ? "best" : PreferredVideoQuality;
        s.PreferredAudioQuality = PreferredAudioQuality == "Best" ? "best" : PreferredAudioQuality;
        s.DefaultSubtitleLanguage = LabelToLang(DefaultSubtitles);
        s.FilenameTemplate = FilenameTemplate;
        s.Proxy = Proxy;
        s.CookiesFile = CookiesFile;
        s.YtDlpPath = YtDlpPath;
        s.FfmpegPath = FfmpegPath;
        s.Theme = Theme;
        s.Density = Density;
        s.DiagnosticLogging = DiagnosticLogging;
        s.VerboseOutput = VerboseOutput;
        return s;
    }

    [RelayCommand]
    private async Task SaveChanges()
    {
        try { _startup.Apply(StartWithWindows, StartMinimized); }
        catch (Exception ex) { StatusMessage = $"Could not change Windows startup: {ex.Message}"; return; }
        await _settings.SaveAsync(BuildSettings());
        AppLog.Verbose = DiagnosticLogging || VerboseOutput;
        _coordinator.Refresh();
        StatusMessage = "Settings saved.";
        DownloadsPath = DefaultDownloadFolder;
    }

    [RelayCommand]
    private async Task Revert()
    {
        await _settings.LoadAsync();
        LoadFrom(_settings.Current);
        StatusMessage = "Reverted to last saved settings.";
    }

    [RelayCommand]
    private async Task ResetDefaults()
    {
        var defaults = new AppSettings();
        try { _startup.Apply(defaults.StartWithWindows, defaults.StartMinimized); }
        catch (Exception ex) { StatusMessage = $"Could not reset Windows startup: {ex.Message}"; return; }
        LoadFrom(defaults);
        await _settings.SaveAsync(defaults);
        _coordinator.Refresh();
        StatusMessage = "Settings reset to defaults.";
    }

    private bool CanCheckUpdates() => !IsToolsBusy;

    [RelayCommand(CanExecute = nameof(CanCheckUpdates))]
    private async Task CheckUpdates()
    {
        if (IsToolsBusy) return;
        var updating = HasUpdate;
        if (updating && _coordinator.ActiveJobCount > 0)
        {
            StatusMessage = "Finish or pause active downloads before updating tools.";
            return;
        }
        var operation = ++_toolOperationVersion;
        IsUpdatingTools = updating;
        IsToolsBusy = true;
        StatusMessage = updating ? "Updating tools…" : "Checking for updates…";
        var log = new Progress<string>(message =>
        {
            if (IsToolsBusy && operation == _toolOperationVersion) StatusMessage = message;
        });
        try
        {
            if (updating)
            {
                var result = await _tools.UpdateAllToolsAsync(log);
                await RefreshSystemInfoAsync();
                StatusMessage = result.IsFailure ? $"Update failed: {result.Error}"
                    : HasUpdate ? "Tools refreshed, but updates are still available. Check your configured tool paths."
                    : "Tools updated.";
            }
            else
            {
                var info = await _tools.CheckForUpdatesAsync();
                await RefreshSystemInfoAsync(info);
                StatusMessage = info.UpdateAvailable ? "Tool updates are available." : "All tools are up to date.";
            }
        }
        catch (Exception ex) { StatusMessage = updating ? $"Update failed: {ex.Message}" : $"Update check failed: {ex.Message}"; }
        finally { IsToolsBusy = false; IsUpdatingTools = false; }
    }

    [RelayCommand]
    private void ManageStorage() => _system.OpenFolder(DefaultDownloadFolder);

    [RelayCommand]
    private void ViewLogs()
    {
        Directory.CreateDirectory(AppLog.LogDirectory);
        _system.OpenFolder(AppLog.LogDirectory);
    }

    [RelayCommand] private void SetTheme(string theme) => Theme = theme;
    [RelayCommand] private void SetDensity(string density) => Density = density;

    partial void OnThemeChanged(string value) => ThemeManager.ApplyTheme(value);

    [RelayCommand]
    private void InsertTemplateVariable(string token) => FilenameTemplate += token;

    [RelayCommand]
    private void Browse()
    {
        var picked = _system.PickFolder(DefaultDownloadFolder);
        if (picked is not null)
        {
            DefaultDownloadFolder = picked;
        }
    }

    [RelayCommand]
    private void BrowseCookies()
    {
        var picked = _system.PickFile("Cookies (*.txt)|*.txt|All files (*.*)|*.*");
        if (picked is not null)
        {
            CookiesFile = picked;
        }
    }

    [RelayCommand]
    private void BrowseYtDlp()
    {
        var picked = _system.PickFile("yt-dlp (yt-dlp.exe)|yt-dlp.exe|Executables (*.exe)|*.exe");
        if (picked is not null)
        {
            YtDlpPath = picked;
        }
    }

    [RelayCommand]
    private void BrowseFfmpeg()
    {
        var picked = _system.PickFile("ffmpeg (ffmpeg.exe)|ffmpeg.exe|Executables (*.exe)|*.exe");
        if (picked is not null)
        {
            FfmpegPath = picked;
        }
    }

    private async Task InitializeSystemInfoAsync()
    {
        IsToolsBusy = true;
        try { await RefreshSystemInfoAsync(); }
        catch (Exception ex)
        {
            YtDlpBadgeText = "UNKNOWN";
            FfmpegBadgeText = "UNKNOWN";
            YtDlpVersion = "unavailable";
            FfmpegVersion = "unavailable";
            StatusMessage = $"Could not load tool information: {ex.Message}";
        }
        finally { IsToolsBusy = false; }
    }

    private async Task RefreshSystemInfoAsync(ToolUpdateInfo? freshUpdateInfo = null)
    {
        var statuses = await _tools.GetStatusAsync();
        var yt = statuses.FirstOrDefault(s => s.Name == "yt-dlp");
        var ff = statuses.FirstOrDefault(s => s.Name == "ffmpeg");
        IsYtDlpAvailable = yt?.Available == true;
        IsFfmpegAvailable = ff?.Available == true;
        YtDlpVersion = yt?.Version ?? (yt?.Available == true ? "installed" : "not found");
        FfmpegVersion = ExtractFfmpegVersion(ff?.Version) ?? (ff?.Available == true ? "installed" : "not found");

        var updateInfo = freshUpdateInfo ?? _tools.LastUpdateInfo;
        if (updateInfo.LastChecked == DateTimeOffset.MinValue
            || !string.Equals(updateInfo.CurrentYtDlpVersion, yt?.Version, StringComparison.Ordinal)
            || !string.Equals(updateInfo.CurrentFfmpegVersion, ff?.Version, StringComparison.Ordinal))
        {
            updateInfo = await _tools.CheckForUpdatesAsync();
        }

        ApplyUpdateInfo(updateInfo);
    }

    private void ApplyUpdateInfo(ToolUpdateInfo info)
    {
        HasUpdate = info.UpdateAvailable;
        if (info.LastChecked > DateTimeOffset.MinValue)
        {
            LastChecked = Formatting.ShortDate(info.LastChecked);
        }

        if (!IsYtDlpAvailable)
        {
            YtDlpBadgeText = "MISSING";
            IsYtDlpUpdated = false;
        }
        else if (info.YtDlpUpdateAvailable)
        {
            YtDlpBadgeText = $"NEW ({info.LatestYtDlpVersion})";
            IsYtDlpUpdated = false;
        }
        else
        {
            YtDlpBadgeText = "LATEST";
            IsYtDlpUpdated = true;
        }

        FfmpegBadgeText = IsFfmpegAvailable ? "READY" : "MISSING";
    }

    private static string? ExtractFfmpegVersion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        // "ffmpeg version n7.0 Copyright ..." → "n7.0"
        var parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var idx = Array.IndexOf(parts, "version");
        return idx >= 0 && idx + 1 < parts.Length ? parts[idx + 1] : raw;
    }

    private static string KbpsToLabel(int kbps) => kbps switch
    {
        <= 0 => "0 (Unlimited)",
        512 => "512 KB/s",
        1024 => "1 MB/s",
        2048 => "2 MB/s",
        5120 => "5 MB/s",
        10240 => "10 MB/s",
        20480 => "20 MB/s",
        51200 => "50 MB/s",
        _ => $"{kbps} KB/s"
    };

    private static int LabelToKbps(string label) => label switch
    {
        "512 KB/s" => 512,
        "1 MB/s" => 1024,
        "2 MB/s" => 2048,
        "5 MB/s" => 5120,
        "10 MB/s" => 10240,
        "20 MB/s" => 20480,
        "50 MB/s" => 51200,
        _ => label.EndsWith(" KB/s", StringComparison.Ordinal)
            && int.TryParse(label[..^5], out var kbps) && kbps > 0 ? kbps : 0
    };

    private static string LangToLabel(string lang) => lang switch
    {
        "en" => "English",
        "es" => "Spanish",
        "fr" => "French",
        "de" => "German",
        "ja" => "Japanese",
        "" => "None",
        _ => lang
    };

    private static string LabelToLang(string label) => label switch
    {
        "English" => "en",
        "Spanish" => "es",
        "French" => "fr",
        "German" => "de",
        "Japanese" => "ja",
        "None" => "",
        _ => label
    };
}
