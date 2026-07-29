using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Yoink.Presentation.Controls;

/// <summary>Sidebar navigation row: icon + label, with an active-state accent bar and orange highlight.</summary>
public sealed class NavItemButton : Button
{
    static NavItemButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NavItemButton), new FrameworkPropertyMetadata(typeof(NavItemButton)));
    }

    public static readonly DependencyProperty IconDataProperty =
        DependencyProperty.Register(nameof(IconData), typeof(Geometry), typeof(NavItemButton));

    public Geometry? IconData
    {
        get => (Geometry?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(NavItemButton));

    public string? Label
    {
        get => (string?)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(NavItemButton));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }
}
