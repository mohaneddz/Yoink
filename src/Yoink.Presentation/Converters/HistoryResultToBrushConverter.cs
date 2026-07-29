using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Yoink.Presentation.ViewModels.Models;

namespace Yoink.Presentation.Converters;

public sealed class HistoryResultToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resources = System.Windows.Application.Current.Resources;
        return value switch
        {
            HistoryResult.Success => (Brush)resources["SuccessBrush"],
            HistoryResult.Failed => (Brush)resources["ErrorBrush"],
            HistoryResult.Cancelled => (Brush)resources["ErrorBrush"],
            HistoryResult.Queued => (Brush)resources["OrangePrimaryBrush"],
            _ => (Brush)resources["TextMutedBrush"]
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
