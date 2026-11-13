using Yoink.Domain.Media;

namespace Yoink.Infrastructure.Ytdlp;

/// <summary>Briefly retains inspected download links in memory so Start need not extract them again.</summary>
public sealed class PreparedDownloadCache
{
    private readonly object _sync = new();
    private readonly TimeProvider _clock;
    private readonly Dictionary<(string Url, string Proxy, string Cookies), Entry> _entries = new();
    private sealed record Entry(string Json, DateTimeOffset Expires);

    public PreparedDownloadCache() : this(TimeProvider.System) { }
    internal PreparedDownloadCache(TimeProvider clock) => _clock = clock;

    internal void Store(string url, string? proxy, string? cookies, string json, MediaMetadata media)
    {
        if (media.IsLive || !media.Formats.Any(f => f.HasVideo || f.HasAudio)) return;
        lock (_sync)
        {
            foreach (var key in _entries.Where(e => e.Value.Expires <= _clock.GetUtcNow()).Select(e => e.Key).ToList())
                _entries.Remove(key);
            if (_entries.Count >= 32) _entries.Remove(_entries.MinBy(e => e.Value.Expires).Key);
            _entries[(url.Trim(), proxy ?? "", cookies ?? "")] = new(json, _clock.GetUtcNow().AddMinutes(2));
        }
    }

    internal string? Get(string url, string? proxy, string? cookies)
    {
        lock (_sync)
        {
            var key = (url.Trim(), proxy ?? "", cookies ?? "");
            if (_entries.TryGetValue(key, out var entry) && entry.Expires > _clock.GetUtcNow()) return entry.Json;
            _entries.Remove(key);
            return null;
        }
    }

    internal void Remove(string url, string? proxy, string? cookies)
    {
        lock (_sync) _entries.Remove((url.Trim(), proxy ?? "", cookies ?? ""));
    }
}
