using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Yoink.Presentation.Converters;

/// <summary>Converts a bool to GridLength(0) when false, or GridLength(parameter) when true. Parameter defaults to "360".</summary>
public sealed class BoolToGridLengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isOpen = value is true;
        if (!isOpen)
        {
            return new GridLength(0);
        }

        var width = parameter is string s && double.TryParse(s, out var w) ? w : 360d;
        return new GridLength(width);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
