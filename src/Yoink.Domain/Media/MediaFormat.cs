namespace Yoink.Domain.Media;

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

    public bool HasVideo => VideoCodec != "none" && (!string.IsNullOrEmpty(VideoCodec) || Height is > 0 || Width is > 0);
    public bool HasAudio => !string.IsNullOrEmpty(AudioCodec) && AudioCodec != "none";
    public bool IsHdr => !string.IsNullOrEmpty(DynamicRange) && !DynamicRange.Equals("SDR", StringComparison.OrdinalIgnoreCase);

    /// <summary>Best-known byte size (exact if available, otherwise the yt-dlp approximation).</summary>
    public long? EffectiveSizeBytes => FilesizeBytes is > 0 ? FilesizeBytes : FilesizeApproxBytes is > 0 ? FilesizeApproxBytes : null;

    /// <summary>Estimate the source stream size without fetching media or extra manifests.</summary>
    public long? EstimateSizeBytes(double? durationSeconds)
    {
        if (EffectiveSizeBytes is { } known) return known;
        var bitrate = TotalBitrateKbps is > 0 ? TotalBitrateKbps.Value
            : (VideoBitrateKbps ?? 0) + (AudioBitrateKbps ?? 0);
        var bytes = bitrate * 1000 / 8 * (durationSeconds ?? 0);
        return double.IsFinite(bytes) && bytes > 0 && bytes < long.MaxValue
            ? (long)Math.Round(bytes) : null;
    }
}
