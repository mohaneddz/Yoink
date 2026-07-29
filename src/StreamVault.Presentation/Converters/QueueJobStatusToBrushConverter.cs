using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StreamVault.Presentation.ViewModels.Models;

namespace StreamVault.Presentation.Converters;

/// <summary>Maps a job status to the Dispatch status color: orange=active, green=success, red=failure, gray=inactive.</summary>
public sealed class QueueJobStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resources = System.Windows.Application.Current.Resources;
        return value switch
        {
            QueueJobStatus.DownloadingVideo or QueueJobStatus.DownloadingAudio or QueueJobStatus.DownloadingSubtitles
                or QueueJobStatus.Merging or QueueJobStatus.Converting or QueueJobStatus.WritingMetadata
                or QueueJobStatus.Paused or QueueJobStatus.Retrying or QueueJobStatus.Inspecting
                => (Brush)resources["OrangePrimaryBrush"],
            QueueJobStatus.Completed => (Brush)resources["SuccessBrush"],
            QueueJobStatus.Failed or QueueJobStatus.Cancelled => (Brush)resources["ErrorBrush"],
            _ => (Brush)resources["TextMutedBrush"]
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
