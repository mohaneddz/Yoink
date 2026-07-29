using System.Windows;

namespace Yoink.Presentation.Views.Dialogs;

public enum ExitChoice
{
    Return,
    PauseAndExit,
    CancelDownloadsAndExit
}

/// <summary>Spec §8.7 exit confirmation, shown when the queue has active downloads at close time.</summary>
public partial class ExitConfirmationDialog : Window
{
    public ExitChoice Choice { get; private set; } = ExitChoice.Return;

    public ExitConfirmationDialog(int activeDownloadCount)
    {
        InitializeComponent();
        HeadlineText.Text = activeDownloadCount == 1
            ? "1 DOWNLOAD IS STILL ACTIVE"
            : $"{activeDownloadCount} DOWNLOADS ARE STILL ACTIVE";
    }

    private void PauseAndExit_Click(object sender, RoutedEventArgs e)
    {
        Choice = ExitChoice.PauseAndExit;
        Close();
    }

    private void CancelDownloadsAndExit_Click(object sender, RoutedEventArgs e)
    {
        Choice = ExitChoice.CancelDownloadsAndExit;
        Close();
    }

    private void Return_Click(object sender, RoutedEventArgs e)
    {
        Choice = ExitChoice.Return;
        Close();
    }
}
