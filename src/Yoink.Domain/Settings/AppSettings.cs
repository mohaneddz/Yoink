namespace Yoink.Domain.Settings;

/// <summary>User-configurable application settings, persisted as JSON.</summary>
public sealed class AppSettings
{
    // General
    public bool HidePrivateVideos { get; set; }
    public string DefaultDownloadFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Videos", "Yoink");
    public int SimultaneousDownloads { get; set; } = 3;
    public int DownloadSpeedLimitKbps { get; set; }
    public bool DesktopNotifications { get; set; } = true;
    public bool SoundNotifications { get; set; }
    public int ConcurrentFragments { get; set; } = 8;
    public bool ReuseRecentInspection { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool MinimizeToTray { get; set; }
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool ResumeInterruptedDownloads { get; set; }
    public bool PreventSleepWhileDownloading { get; set; } = true;

    // Formats
    public string PreferredVideoContainer { get; set; } = "mp4";
    public string PreferredAudioContainer { get; set; } = "m4a";
    public string PreferredVideoQuality { get; set; } = "best";
    public Dictionary<string, int> SourceVideoQualities { get; set; } = new();
    public string PreferredAudioQuality { get; set; } = "best";
    public string DefaultSubtitleLanguage { get; set; } = "en";

    // Naming
    public string FilenameTemplate { get; set; } = "%(title)s [%(id)s].%(ext)s";

    // Tools & paths (empty = auto-resolve/provision)
    public string YtDlpPath { get; set; } = string.Empty;
    public string FfmpegPath { get; set; } = string.Empty;
    public string CookiesFile { get; set; } = string.Empty;
    public string Proxy { get; set; } = string.Empty;

    // Appearance
    public string Theme { get; set; } = "Light";
    public string Density { get; set; } = "Comfortable";

    // Window
    public double WindowWidth { get; set; } = 1440;
    public double WindowHeight { get; set; } = 960;
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public bool IsWindowMaximized { get; set; }

    // Advanced
    public bool DiagnosticLogging { get; set; }
    public bool VerboseOutput { get; set; }
}
