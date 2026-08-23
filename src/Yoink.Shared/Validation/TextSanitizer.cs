namespace Yoink.Shared.Validation;

/// <summary>
/// Utility methods for cleaning and normalizing user text inputs.
/// </summary>
public static class TextSanitizer
{
    private static readonly string[] NewlineSeparators = ["\r\n", "\r", "\n"];

    /// <summary>
    /// Truncates multiline text to only the first non-empty line and trims leading/trailing whitespace.
    /// </summary>
    public static string TruncateToSingleLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = text.Split(NewlineSeparators, StringSplitOptions.RemoveEmptyEntries);
        var firstLine = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        return firstLine?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Normalizes handles (@name) and channel domain fragments into full YouTube channel URLs.
    /// </summary>
    public static string NormalizeChannelInput(string? input)
    {
        var raw = TruncateToSingleLine(input);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        if (raw.StartsWith("@"))
        {
            return $"https://www.youtube.com/{raw}";
        }
        if (raw.StartsWith("youtube.com", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("www.youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            return $"https://{raw}";
        }
        if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (raw.Contains('/'))
            {
                return $"https://www.youtube.com/{raw.TrimStart('/')}";
            }
            return $"https://www.youtube.com/@{raw}";
        }
        return raw;
    }
}
