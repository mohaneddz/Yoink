using StreamVault.Shared;

namespace StreamVault.Application.Downloads;

/// <summary>A progress snapshot emitted while a download runs (parsed from yt-dlp output).</summary>
public sealed class DownloadProgress
{
    public DownloadStage Stage { get; init; }

    /// <summary>0-100 for the current file, or null when indeterminate.</summary>
    public double? Percent { get; init; }
    public long? DownloadedBytes { get; init; }
    public long? TotalBytes { get; init; }
    public double? SpeedBytesPerSecond { get; init; }
    public double? EtaSeconds { get; init; }
    public string? Destination { get; init; }

    /// <summary>Raw log line (for the live log panel), if this snapshot came from stdout/stderr.</summary>
    public string? RawLine { get; init; }

    public static DownloadProgress Log(string line, DownloadStage stage) => new() { Stage = stage, RawLine = line };
}

/// <summary>Terminal result of a download run.</summary>
public sealed class DownloadResult
{
    public required DownloadOutcome Outcome { get; init; }
    public int ExitCode { get; init; }
    public string? OutputPath { get; init; }
    public long FileSizeBytes { get; init; }
    public string? ErrorMessage { get; init; }
}
