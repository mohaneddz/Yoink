using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Yoink.Presentation.ViewModels.Models;

namespace Yoink.Presentation.ViewModels;

/// <summary>Backs the editable "Download selected" dialog: format, quality, subtitles, and save folder.</summary>
public partial class BatchDownloadDialogViewModel : ObservableObject
{
    public BatchDownloadDialogViewModel(int count, string container, string outputFolder)
    {
        Count = count;
        HeadlineText = count == 1 ? "DOWNLOAD 1 VIDEO" : $"DOWNLOAD {count} VIDEOS";
        _container = ContainerOptions.FirstOrDefault(c => string.Equals(c, container, StringComparison.OrdinalIgnoreCase)) ?? "MP4";
        _outputFolder = outputFolder;
    }

    public int Count { get; }
    public string HeadlineText { get; }

    public ObservableCollection<string> ContainerOptions { get; } = new() { "MP4", "MKV", "WEBM" };
    public ObservableCollection<string> QualityOptions { get; } =
        new() { "Best available", "2160p", "1440p", "1080p", "720p", "480p", "360p" };
    public ObservableCollection<string> SubtitleOptions { get; } =
        new() { "None", "English", "Spanish", "French", "German", "Japanese", "All" };

    [ObservableProperty] private string _container = "MP4";
    [ObservableProperty] private string _quality = "Best available";
    [ObservableProperty] private string _subtitleLanguage = "None";
    [ObservableProperty] private string _outputFolder = string.Empty;

    public BatchDownloadOptions ToOptions() => new(Container, Quality, SubtitleLanguage, OutputFolder);
}
