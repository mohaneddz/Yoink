using System.Globalization;
using System.Windows.Data;
using StreamVault.Presentation.ViewModels.Models;

namespace StreamVault.Presentation.Converters;

public sealed class QueueJobStatusToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            QueueJobStatus.Inspecting => "INSPECTING",
            QueueJobStatus.Waiting => "WAITING",
            QueueJobStatus.Queued => "QUEUED",
            QueueJobStatus.DownloadingVideo => "DOWNLOADING",
            QueueJobStatus.DownloadingAudio => "DOWNLOADING",
            QueueJobStatus.DownloadingSubtitles => "DOWNLOADING",
            QueueJobStatus.Merging => "MERGING",
            QueueJobStatus.Converting => "CONVERTING",
            QueueJobStatus.WritingMetadata => "WRITING METADATA",
            QueueJobStatus.Completed => "COMPLETED",
            QueueJobStatus.Paused => "PAUSED",
            QueueJobStatus.Cancelled => "CANCELLED",
            QueueJobStatus.Failed => "FAILED",
            QueueJobStatus.Retrying => "RETRYING",
            _ => value?.ToString()?.ToUpperInvariant() ?? string.Empty
        };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
