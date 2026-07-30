using StreamVault.Shared;

namespace StreamVault.Domain.Entities;

/// <summary>A persisted downloaded file tracked by the Library.</summary>
public sealed class LibraryEntry
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public DownloadKind Kind { get; set; }
    public string Format { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string ResolutionText { get; set; } = string.Empty;
    public string VideoCodec { get; set; } = string.Empty;
    public string AudioCodec { get; set; } = string.Empty;
    public double? Fps { get; set; }
    public double? DurationSeconds { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTimeOffset DownloadedAt { get; set; } = DateTimeOffset.Now;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string TagsCsv { get; set; } = string.Empty;
}
