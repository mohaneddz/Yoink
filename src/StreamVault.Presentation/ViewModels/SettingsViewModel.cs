using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVault.Application.Abstractions;
using StreamVault.Domain.Settings;
using StreamVault.Presentation.Services;
using StreamVault.Presentation.ViewModels.Models;

namespace StreamVault.Presentation.ViewModels;

/// <summary>Backs the Settings page: edit, persist, and reset application configuration.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IToolProvisioner _tools;
    private readonly ISystemAccess _system;

    public SettingsViewModel(ISettingsService settings, IToolProvisioner tools, ISystemAccess system)
    {
        _settings = settings;
        _tools = tools;
        _system = system;
        LoadFrom(settings.Current);
        _ = RefreshSystemInfoAsync();
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
        new() { "0 (Unlimited)", "512 KB/s", "1 MB/s", "2 MB/s", "5 MB/s", "10 MB/s" };
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

    private void LoadFrom(AppSettings s)
    {
        DefaultDownloadFolder = s.DefaultDownloadFolder;
        SimultaneousDownloads = s.SimultaneousDownloads.ToString();
        DownloadSpeedLimit = KbpsToLabel(s.DownloadSpeedLimitKbps);
        DesktopNotifications = s.DesktopNotifications;
        SoundNotifications = s.SoundNotifications;
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
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamVault");
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
        await _settings.SaveAsync(BuildSettings());
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
        LoadFrom(defaults);
        await _settings.SaveAsync(defaults);
        StatusMessage = "Settings reset to defaults.";
    }

    [RelayCommand]
    private async Task CheckUpdates()
    {
        StatusMessage = "Checking for updates…";
        var log = new Progress<string>(m => StatusMessage = m);
        await _tools.UpdateYtDlpAsync(log);
        await RefreshSystemInfoAsync();
        LastChecked = Formatting.ShortDate(DateTimeOffset.Now);
        StatusMessage = "Up to date.";
    }

    [RelayCommand]
    private void ManageStorage() => _system.OpenFolder(DefaultDownloadFolder);

    [RelayCommand]
    private void ViewLogs()
    {
        var dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamVault");
        _system.OpenFolder(dataDir);
    }

    [RelayCommand] private void SetTheme(string theme) => Theme = theme;
    [RelayCommand] private void SetDensity(string density) => Density = density;

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

    private async Task RefreshSystemInfoAsync()
    {
        var statuses = await _tools.GetStatusAsync();
        var yt = statuses.FirstOrDefault(s => s.Name == "yt-dlp");
        var ff = statuses.FirstOrDefault(s => s.Name == "ffmpeg");
        YtDlpVersion = yt?.Version ?? (yt?.Available == true ? "installed" : "not found");
        FfmpegVersion = ExtractFfmpegVersion(ff?.Version) ?? (ff?.Available == true ? "installed" : "not found");
        LastChecked = Formatting.ShortDate(DateTimeOffset.Now);
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
        _ => $"{kbps} KB/s"
    };

    private static int LabelToKbps(string label) => label switch
    {
        "512 KB/s" => 512,
        "1 MB/s" => 1024,
        "2 MB/s" => 2048,
        "5 MB/s" => 5120,
        "10 MB/s" => 10240,
        _ => 0
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
