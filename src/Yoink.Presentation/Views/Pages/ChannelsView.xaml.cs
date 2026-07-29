using System.Windows.Controls;
using Yoink.Presentation.ViewModels;

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
}
