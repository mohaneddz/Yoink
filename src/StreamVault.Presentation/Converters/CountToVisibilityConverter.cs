using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StreamVault.Presentation.Converters;

/// <summary>
/// Maps an item count to visibility for empty-state UI.
/// By default: Visible when the count is 0 (the list is empty), Collapsed otherwise.
/// Pass ConverterParameter="Invert" to flip it (Visible only when there are items).
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value switch
        {
            int i => i,
            null => 0,
            _ => int.TryParse(value.ToString(), out var n) ? n : 0
        };

        var invert = string.Equals(parameter?.ToString(), "Invert", StringComparison.OrdinalIgnoreCase);
        var isEmpty = count == 0;
        return (invert ? !isEmpty : isEmpty) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
