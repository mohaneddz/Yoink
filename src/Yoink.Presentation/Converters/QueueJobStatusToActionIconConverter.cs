using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Yoink.Presentation.ViewModels.Models;

namespace Yoink.Presentation.Converters;

/// <summary>Picks the row action icon: pause (active jobs), retry (failed jobs), or play (everything else).</summary>
public sealed class QueueJobStatusToActionIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resources = System.Windows.Application.Current.Resources;
        return value switch
        {
            QueueJobStatus.DownloadingVideo or QueueJobStatus.DownloadingAudio or QueueJobStatus.DownloadingSubtitles
                or QueueJobStatus.Merging or QueueJobStatus.Converting or QueueJobStatus.WritingMetadata
                => (Geometry)resources["IconPause"],
            QueueJobStatus.Failed => (Geometry)resources["IconRefresh"],
            _ => (Geometry)resources["IconPlay"]
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
