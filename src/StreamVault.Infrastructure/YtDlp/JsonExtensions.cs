using System.Globalization;
using System.Text.Json;

namespace StreamVault.Infrastructure.Ytdlp;

/// <summary>Null-tolerant readers over yt-dlp's JSON (fields are frequently absent or null).</summary>
internal static class JsonExtensions
{
    public static string? GetStr(this JsonElement el, string name)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
        {
            return p.GetString();
        }

        return null;
    }

    public static long? GetLong(this JsonElement el, string name)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p))
        {
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var v))
            {
                return v;
            }

            if (p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s))
            {
                return s;
            }
        }

        return null;
    }

    public static double? GetDouble(this JsonElement el, string name)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p))
        {
            if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var v))
            {
                return v;
            }

            if (p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s))
            {
                return s;
            }
        }

        return null;
    }

    public static int? GetInt(this JsonElement el, string name) => (int?)el.GetLong(name);

    public static bool GetBool(this JsonElement el, string name)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p))
        {
            return p.ValueKind == JsonValueKind.True;
        }

        return false;
    }

    /// <summary>Parse a yt-dlp upload_date (YYYYMMDD) to a DateOnly.</summary>
    public static DateOnly? GetDate(this JsonElement el, string name)
    {
        var s = el.GetStr(name);
        if (!string.IsNullOrEmpty(s) && s.Length == 8 &&
            DateOnly.TryParseExact(s, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d;
        }

        return null;
    }
}
