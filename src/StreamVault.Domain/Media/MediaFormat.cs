namespace StreamVault.Domain.Media;

/// <summary>A single downloadable stream/format reported by yt-dlp for a media item.</summary>
public sealed class MediaFormat
{
    public required string FormatId { get; init; }
    public string? FormatNote { get; init; }
    public string? Extension { get; init; }
    public string? Resolution { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public double? Fps { get; init; }
    public string? VideoCodec { get; init; }
    public string? AudioCodec { get; init; }
    public long? FilesizeBytes { get; init; }
    public long? FilesizeApproxBytes { get; init; }
    public double? TotalBitrateKbps { get; init; }
    public double? VideoBitrateKbps { get; init; }
    public double? AudioBitrateKbps { get; init; }
    public int? AudioChannels { get; init; }
    public string? DynamicRange { get; init; }

    public bool HasVideo => !string.IsNullOrEmpty(VideoCodec) && VideoCodec != "none";
    public bool HasAudio => !string.IsNullOrEmpty(AudioCodec) && AudioCodec != "none";
    public bool IsHdr => !string.IsNullOrEmpty(DynamicRange) && !DynamicRange.Equals("SDR", StringComparison.OrdinalIgnoreCase);

    /// <summary>Best-known byte size (exact if available, otherwise the yt-dlp approximation).</summary>
    public long? EffectiveSizeBytes => FilesizeBytes ?? FilesizeApproxBytes;
}
