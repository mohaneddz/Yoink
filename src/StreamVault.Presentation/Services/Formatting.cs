using System.Globalization;

namespace StreamVault.Presentation.Services;

/// <summary>Human-friendly formatting for sizes, counts, durations, speeds, and dates.</summary>
public static class Formatting
{
    public static string Bytes(long? bytes)
    {
        if (bytes is null or <= 0)
        {
            return "—";
        }

        double b = bytes.Value;
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var i = 0;
        while (b >= 1024 && i < units.Length - 1)
        {
            b /= 1024;
            i++;
        }

        return i == 0 ? $"{b:0} {units[i]}" : $"{b:0.##} {units[i]}";
    }

    public static string Count(long? count)
    {
        if (count is null)
        {
            return "—";
        }

        double c = count.Value;
        return c switch
        {
            >= 1_000_000_000 => $"{c / 1_000_000_000:0.#}B",
            >= 1_000_000 => $"{c / 1_000_000:0.#}M",
            >= 1_000 => $"{c / 1_000:0.#}K",
            _ => c.ToString("0", CultureInfo.InvariantCulture)
        };
    }

    public static string FullCount(long? count) =>
        count?.ToString("#,0", CultureInfo.InvariantCulture) ?? "—";

    public static string Duration(double? seconds)
    {
        if (seconds is null or < 0)
        {
            return "—";
        }

        var ts = TimeSpan.FromSeconds(seconds.Value);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes}:{ts.Seconds:00}";
    }

    public static string Speed(double? bytesPerSecond) =>
        bytesPerSecond is null or <= 0 ? "—" : $"{Bytes((long)bytesPerSecond.Value)}/s";

    public static string Eta(double? seconds)
    {
        if (seconds is null or < 0)
        {
            return "—";
        }

        var ts = TimeSpan.FromSeconds(seconds.Value);
        if (ts.TotalHours >= 1)
        {
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        }

        return ts.TotalMinutes >= 1 ? $"{ts.Minutes}m {ts.Seconds}s" : $"{ts.Seconds}s";
    }

    public static string RelativeDate(DateTimeOffset? when)
    {
        if (when is null)
        {
            return "—";
        }

        var span = DateTimeOffset.Now - when.Value;
        return span switch
        {
            { TotalSeconds: < 60 } => "just now",
            { TotalMinutes: < 60 } => $"{(int)span.TotalMinutes}m ago",
            { TotalHours: < 24 } => $"{(int)span.TotalHours}h ago",
            { TotalDays: < 7 } => $"{(int)span.TotalDays}d ago",
            { TotalDays: < 30 } => $"{(int)(span.TotalDays / 7)}w ago",
            { TotalDays: < 365 } => $"{(int)(span.TotalDays / 30)}mo ago",
            _ => $"{(int)(span.TotalDays / 365)}y ago"
        };
    }

    public static string RelativeDate(DateOnly? date) =>
        date is null ? "—" : RelativeDate(new DateTimeOffset(date.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

    public static string ShortDate(DateOnly? date) =>
        date?.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) ?? "—";

    public static string ShortDate(DateTimeOffset? when) =>
        when?.ToString("MMM d, yyyy • HH:mm", CultureInfo.InvariantCulture) ?? "—";
}
