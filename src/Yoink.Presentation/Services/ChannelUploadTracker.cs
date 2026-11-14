using System.Text.Json;
using Yoink.Domain.Media;

namespace Yoink.Presentation.Services;

/// <summary>Tracks a bounded recent upload window and avoids announcing old uploads on first check.</summary>
public static class ChannelUploadTracker
{
    private const int MaxRecentIds = 50;

    public static (string Snapshot, int NewCount) Compare(string? previousJson, IReadOnlyList<PlaylistEntry> entries)
    {
        var current = entries.Select(e => e.Id).Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).Take(MaxRecentIds).ToArray();
        var snapshot = JsonSerializer.Serialize(current);
        if (string.IsNullOrWhiteSpace(previousJson) || previousJson == "[]")
            return (snapshot, 0);

        try
        {
            var previous = JsonSerializer.Deserialize<string[]>(previousJson);
            if (previous is null || previous.Length == 0) return (snapshot, 0);
            var known = previous.ToHashSet(StringComparer.Ordinal);
            return (snapshot, current.Count(id => !known.Contains(id)));
        }
        catch (JsonException)
        {
            return (snapshot, 0);
        }
    }
}
