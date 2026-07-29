using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace StreamVault.Presentation.Controls;

/// <summary>Attached behaviors for buttons.</summary>
public static class ButtonBehaviors
{
    /// <summary>When true, left-clicking the button opens its own <see cref="FrameworkElement.ContextMenu"/>
    /// anchored to the button (used for inline "more actions" buttons).</summary>
    public static readonly DependencyProperty OpensContextMenuProperty =
        DependencyProperty.RegisterAttached(
            "OpensContextMenu", typeof(bool), typeof(ButtonBehaviors),
            new PropertyMetadata(false, OnOpensContextMenuChanged));

    public static bool GetOpensContextMenu(DependencyObject obj) => (bool)obj.GetValue(OpensContextMenuProperty);
    public static void SetOpensContextMenu(DependencyObject obj, bool value) => obj.SetValue(OpensContextMenuProperty, value);

    private static void OnOpensContextMenuChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ButtonBase button)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            button.Click += Button_Click;
        }
        else
        {
            button.Click -= Button_Click;
        }
    }

    private static void Button_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { ContextMenu: { } menu } fe)
        {
            menu.PlacementTarget = fe;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
