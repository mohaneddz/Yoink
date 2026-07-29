using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVault.Presentation.ViewModels.Models;

namespace StreamVault.Presentation.ViewModels;

/// <summary>Backs the Settings page (spec section 7.7).</summary>
public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private SettingsSection _selectedSection = SettingsSection.General;

    [RelayCommand]
    private void SelectSection(SettingsSection section) => SelectedSection = section;

    // General
    [ObservableProperty] private string _defaultDownloadFolder = @"C:\Users\Alex\Videos\StreamVault";
    [ObservableProperty] private string _simultaneousDownloads = "3";
    [ObservableProperty] private string _downloadSpeedLimit = "0 (Unlimited)";
    [ObservableProperty] private bool _desktopNotifications = true;
    [ObservableProperty] private bool _soundNotifications = true;
    [ObservableProperty] private string _preferredVideoContainer = "MP4";
    [ObservableProperty] private string _preferredAudioContainer = "Audio container";
    [ObservableProperty] private string _preferredVideoQuality = "Best Quality (4K)";
    [ObservableProperty] private string _preferredAudioQuality = "320 kbps";
    [ObservableProperty] private string _defaultSubtitles = "English (Auto)";
    [ObservableProperty] private string _filenameTemplate = "%(title)s - %(uploader)s - %(upload_date)s - %(id)s";
    [ObservableProperty] private string _proxy = string.Empty;
    [ObservableProperty] private string _cookiesFile = string.Empty;
    [ObservableProperty] private string _ytDlpPath = @"C:\Tools\yt-dlp\yt-dlp.exe";
    [ObservableProperty] private string _ffmpegPath = @"C:\Tools\ffmpeg\ffmpeg.exe";

    // System info
    [ObservableProperty] private string _appVersion = "1.3.0";
    [ObservableProperty] private string _ytDlpVersion = "2024.05.27";
    [ObservableProperty] private string _ffmpegVersion = "6.1.1";
    [ObservableProperty] private string _lastChecked = "May 22, 2025 09:12";

    // Storage
    [ObservableProperty] private string _downloadsPath = @"C:\Users\Alex\Videos\StreamVault";
    [ObservableProperty] private string _tempPath = @"C:\Users\Alex\AppData\Local\StreamVault\Temp";
    [ObservableProperty] private string _cachePath = @"C:\Users\Alex\AppData\Local\StreamVault\Cache";

    // Appearance
    [ObservableProperty] private string _theme = "Dark";
    [ObservableProperty] private string _density = "Comfortable";

    // Advanced
    [ObservableProperty] private bool _diagnosticLogging = true;
    [ObservableProperty] private bool _verboseOutput;

    [RelayCommand] private void SaveChanges() { }
    [RelayCommand] private void Revert() { }
    [RelayCommand] private void ResetDefaults() { }
    [RelayCommand] private void CheckUpdates() { }
    [RelayCommand] private void ManageStorage() { }
    [RelayCommand] private void ViewLogs() { }
    [RelayCommand] private void SetTheme(string theme) => Theme = theme;
    [RelayCommand] private void SetDensity(string density) => Density = density;
    [RelayCommand] private void Browse() { }
}
