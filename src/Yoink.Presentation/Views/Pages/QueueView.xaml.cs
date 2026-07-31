using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Yoink.Presentation.Views.Pages;

public partial class QueueView : UserControl
{
    public QueueView()
    {
        InitializeComponent();
    }

    /// <summary>Opens the row's context menu from the inline "more actions" button.</summary>
    private void RowMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe)
        {
            return;
        }

        var row = FindAncestor<DataGridRow>(fe);
        if (row?.ContextMenu is { } menu)
        {
            menu.PlacementTarget = row;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
