using System.Text;

namespace Yoink.Shared.Validation;

/// <summary>
/// Filename/path-segment sanitization shared by every place the app builds a folder or file name
/// from untrusted text (video titles, playlist names, channel names) — spec section 12 safety rules.
/// yt-dlp sanitizes the leaf filename it generates from its own output template, but folder segments
/// the app itself prepends (e.g. a playlist subfolder) are not covered by that and need this.
/// </summary>
public static class FileNaming
{
    private static readonly char[] InvalidChars = "\"<>|:*?/\\".ToCharArray();

    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>Maximum length for a single sanitized path segment (folder or file name), not the full path.</summary>
    public const int MaxSegmentLength = 150;

    /// <summary>
    /// Sanitizes a single path segment (a folder name or a filename including extension) for safe use
    /// on Windows: replaces forbidden characters, trims trailing dots/spaces, avoids reserved device
    /// names, and caps length while preserving Unicode elsewhere.
    /// </summary>
    public static string SanitizeSegment(string name, string fallback = "untitled")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            builder.Append(InvalidChars.Contains(c) || char.IsControl(c) ? '_' : c);
        }

        var sanitized = builder.ToString().Trim().TrimEnd('.', ' ');

        if (sanitized.Length == 0)
        {
            return fallback;
        }

        if (sanitized.Length > MaxSegmentLength)
        {
            sanitized = sanitized[..MaxSegmentLength].TrimEnd('.', ' ');
        }

        var stem = sanitized;
        var dot = sanitized.LastIndexOf('.');
        if (dot > 0)
        {
            stem = sanitized[..dot];
        }

        return ReservedNames.Contains(stem) ? $"_{sanitized}" : sanitized;
    }
}
