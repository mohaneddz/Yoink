using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StreamVault.Presentation.Converters;

/// <summary>Visible when the bound enum's name matches the string ConverterParameter, Collapsed otherwise.</summary>
public sealed class EnumEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null && parameter is not null && string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
