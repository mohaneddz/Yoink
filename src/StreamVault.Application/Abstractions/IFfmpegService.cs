using StreamVault.Shared;

namespace StreamVault.Application.Abstractions;

/// <summary>Media probe result from ffprobe.</summary>
public sealed record MediaProbe(
    double? DurationSeconds,
    int? Width,
    int? Height,
    double? Fps,
    string? VideoCodec,
    string? AudioCodec,
    long? SizeBytes);

/// <summary>Wraps ffmpeg/ffprobe for probing and format conversion.</summary>
public interface IFfmpegService
{
    Task<MediaProbe?> ProbeAsync(string filePath, CancellationToken ct = default);

    Task<Result<string>> ConvertAsync(
        string inputPath,
        string targetContainer,
        IProgress<string>? log = null,
        CancellationToken ct = default);
}
