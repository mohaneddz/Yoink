using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace StreamVault.Presentation.Controls;

/// <summary>Attached behaviors for DataGrid interaction polish.</summary>
public static class DataGridBehaviors
{
    /// <summary>When true, right-clicking a row selects it (so context menus and
    /// selection-based commands act on the row the user pointed at, with visual feedback).</summary>
    public static readonly DependencyProperty SelectRowOnRightClickProperty =
        DependencyProperty.RegisterAttached(
            "SelectRowOnRightClick", typeof(bool), typeof(DataGridBehaviors),
            new PropertyMetadata(false, OnSelectRowOnRightClickChanged));

    public static bool GetSelectRowOnRightClick(DependencyObject obj) => (bool)obj.GetValue(SelectRowOnRightClickProperty);
    public static void SetSelectRowOnRightClick(DependencyObject obj, bool value) => obj.SetValue(SelectRowOnRightClickProperty, value);

    private static void OnSelectRowOnRightClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid grid)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            grid.PreviewMouseRightButtonDown += Grid_PreviewMouseRightButtonDown;
        }
        else
        {
            grid.PreviewMouseRightButtonDown -= Grid_PreviewMouseRightButtonDown;
        }
    }

    private static void Grid_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var row = FindAncestor<DataGridRow>(source);
        if (row is not null && sender is DataGrid grid)
        {
            grid.SelectedItem = row.Item;
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
