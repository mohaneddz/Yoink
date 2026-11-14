using System.Globalization;
using System.Text.RegularExpressions;

namespace Yoink.Presentation.Services;

/// <summary>Resolve the playlist position before handing an individual URL to yt-dlp.</summary>
public static partial class PlaylistFileNaming
{
    public static string Resolve(string template, int index, string numberingPattern)
    {
        if (string.IsNullOrWhiteSpace(template))
            template = "%(title)s [%(id)s].%(ext)s";

        var number = numberingPattern switch
        {
            "None" => string.Empty,
            "Plain (1, 2, 3)" => index.ToString(CultureInfo.InvariantCulture),
            _ => index.ToString("00", CultureInfo.InvariantCulture)
        };
        var resolved = PlaylistIndex().Replace(template, number);
        return number.Length == 0 && resolved != template
            ? resolved.TrimStart(' ', '-', '_', '.')
            : resolved;
    }

    [GeneratedRegex(@"%\(playlist_index\)(?:0?\d+)?[ds]")]
    private static partial Regex PlaylistIndex();
}
