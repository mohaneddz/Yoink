using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Yoink.Presentation.Converters;

/// <summary>Looks up a Geometry resource (e.g. one of the IconXxx keys in Themes/Icons.xaml) by its string key.</summary>
public sealed class StringToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string key && System.Windows.Application.Current.Resources[key] is Geometry geometry ? geometry : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
