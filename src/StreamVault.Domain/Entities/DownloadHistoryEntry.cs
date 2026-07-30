using StreamVault.Shared;

namespace StreamVault.Domain.Entities;

/// <summary>A persisted record of a completed/attempted download (backs the History page).</summary>
public sealed class DownloadHistoryEntry
{
    public int Id { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public DownloadOutcome Outcome { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Format { get; set; } = string.Empty;
    public string ResolutionText { get; set; } = string.Empty;
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public double? DurationSeconds { get; set; }
    public double? Fps { get; set; }
    public int ExitCode { get; set; }
    public bool Redownloaded { get; set; }
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string LogText { get; set; } = string.Empty;
    public DownloadKind Kind { get; set; }
}
