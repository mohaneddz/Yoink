using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Yoink.Presentation.Views.Shared;

public partial class PageHeaderView : UserControl
{
    private readonly DispatcherTimer _clockTimer;

    public PageHeaderView()
    {
        InitializeComponent();
        UpdateClock();
        _clockTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdateClock(), Dispatcher);
        Unloaded += (_, _) => _clockTimer.Stop();
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText.Text = now.ToString("HH:mm:ss");
        DateText.Text = now.ToString("MMMM d, yyyy").ToUpperInvariant();
    }

    public static readonly DependencyProperty HeaderTitleProperty =
        DependencyProperty.Register(nameof(HeaderTitle), typeof(string), typeof(PageHeaderView));

    public string? HeaderTitle
    {
        get => (string?)GetValue(HeaderTitleProperty);
        set => SetValue(HeaderTitleProperty, value);
    }

    public static readonly DependencyProperty HeaderTaglineProperty =
        DependencyProperty.Register(nameof(HeaderTagline), typeof(string), typeof(PageHeaderView));

    public string? HeaderTagline
    {
        get => (string?)GetValue(HeaderTaglineProperty);
        set => SetValue(HeaderTaglineProperty, value);
    }
}
