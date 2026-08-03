using System.Globalization;
using System.Windows.Data;

namespace Yoink.Presentation.Converters;

/// <summary>Formats a check-frequency in hours as "1 hour" / "6 hours" / "24 hours (daily)".</summary>
public sealed class FrequencyLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int hours
            ? hours switch
            {
                1 => "1 hour",
                24 => "24 hours (daily)",
                _ => $"{hours} hours"
            }
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
