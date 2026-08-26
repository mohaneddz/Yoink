using System.IO;
using System.Windows;
using Microsoft.Win32;
using Yoink.Presentation.ViewModels;
using Yoink.Presentation.ViewModels.Models;

namespace Yoink.Presentation.Views.Dialogs;

/// <summary>Confirms a batch download from the Channels page and lets the user tune format, quality, subtitles, and folder.</summary>
public partial class DownloadSelectedDialog : Window
{
    private readonly BatchDownloadDialogViewModel _vm;

    public bool Confirmed { get; private set; }
    public BatchDownloadOptions Options => _vm.ToOptions();

    public DownloadSelectedDialog(BatchDownloadDialogViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = viewModel;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Multiselect = false };
        if (!string.IsNullOrWhiteSpace(_vm.OutputFolder) && Directory.Exists(_vm.OutputFolder))
        {
            dialog.InitialDirectory = _vm.OutputFolder;
        }

        if (dialog.ShowDialog() == true)
        {
            _vm.OutputFolder = dialog.FolderName;
        }
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
