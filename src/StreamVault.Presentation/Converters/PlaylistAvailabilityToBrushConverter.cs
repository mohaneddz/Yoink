using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StreamVault.Presentation.ViewModels.Models;

namespace StreamVault.Presentation.Converters;

public sealed class PlaylistAvailabilityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resources = System.Windows.Application.Current.Resources;
        return value is PlaylistEntryAvailability.Available
            ? (Brush)resources["SuccessBrush"]
            : (Brush)resources["ErrorBrush"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
