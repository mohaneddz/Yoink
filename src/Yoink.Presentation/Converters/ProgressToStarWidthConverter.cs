using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Yoink.Presentation.Converters;

/// <summary>Converts a 0-100 progress value to a GridLength star weight, so a two-column Grid can render a percent bar without pixel math.</summary>
public sealed class ProgressToStarWidthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var progress = value is double d ? d : 0;
        var isFill = parameter as string != "track";
        var weight = isFill ? progress : 100 - progress;
        return new GridLength(Math.Max(weight, 0.0001), GridUnitType.Star);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
