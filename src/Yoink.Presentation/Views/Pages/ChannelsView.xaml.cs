using System.Windows;
using System.Windows.Controls;
using Yoink.Presentation.ViewModels;
using Yoink.Presentation.Views.Dialogs;

namespace Yoink.Presentation.Views.Pages;

public partial class ChannelsView : UserControl
{
    public ChannelsView()
    {
        InitializeComponent();
    }

    private void CheckFrequencyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ChannelsViewModel vm && sender is ComboBox { SelectedItem: int hours })
        {
            vm.SetCheckFrequencyCommand.Execute(hours);
        }
    }

    private void DownloadSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ChannelsViewModel vm || vm.SelectedUploadCount == 0)
        {
            return;
        }

        var dialog = new DownloadSelectedDialog(vm.SelectedUploadCount, vm.PresetFormatText, vm.PresetQualityText, vm.PresetFolderText)
        {
            Owner = Window.GetWindow(this)
        };
        dialog.ShowDialog();

        if (dialog.Confirmed)
        {
            vm.DownloadSelected();
        }
    }
}
