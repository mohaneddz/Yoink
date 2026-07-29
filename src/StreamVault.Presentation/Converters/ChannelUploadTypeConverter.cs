using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StreamVault.Presentation.ViewModels.Models;

namespace StreamVault.Presentation.Converters;

/// <summary>Multi-purpose converter for the upload-type badge: ConverterParameter selects "bg", "fg", or "label".</summary>
public sealed class ChannelUploadTypeConverter : IValueConverter
{
    private static readonly BrushConverter BrushConv = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ChannelUploadType type)
        {
            return DependencyPropertyUnsetValueFallback(parameter);
        }

        return (parameter as string) switch
        {
            "label" => type switch
            {
                ChannelUploadType.Video => "VIDEO",
                ChannelUploadType.Short => "SHORT",
                ChannelUploadType.Live => "LIVE",
                ChannelUploadType.Playlist => "PLAYLIST",
                _ => string.Empty
            },
            "fg" => (Brush)BrushConv.ConvertFromString(type switch
            {
                ChannelUploadType.Live => "#FFFFFFFF",
                ChannelUploadType.Short => "#FF7C3AED",
                ChannelUploadType.Playlist => "#FF1F6FEB",
                _ => "#FF6D6D68"
            })!,
            _ => (Brush)BrushConv.ConvertFromString(type switch
            {
                ChannelUploadType.Live => "#FFD93025",
                ChannelUploadType.Short => "#FFEDE7FA",
                ChannelUploadType.Playlist => "#FFE3F2FD",
                _ => "#FFEFEFED"
            })!
        };
    }

    private static object DependencyPropertyUnsetValueFallback(object? parameter) => parameter as string == "label" ? string.Empty : Brushes.Transparent;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
