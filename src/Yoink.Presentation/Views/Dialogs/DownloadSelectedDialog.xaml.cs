using System.Windows;

namespace Yoink.Presentation.Views.Dialogs;

/// <summary>Confirms a batch download from the Channels page, showing the count and the preset it will use.</summary>
public partial class DownloadSelectedDialog : Window
{
    public bool Confirmed { get; private set; }

    public DownloadSelectedDialog(int count, string format, string quality, string folder)
    {
        InitializeComponent();
        HeadlineText.Text = count == 1 ? "DOWNLOAD 1 VIDEO" : $"DOWNLOAD {count} VIDEOS";
        FormatText.Text = string.IsNullOrWhiteSpace(format) ? "—" : format;
        QualityText.Text = string.IsNullOrWhiteSpace(quality) ? "—" : quality;
        FolderText.Text = string.IsNullOrWhiteSpace(folder) ? "—" : folder;
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
