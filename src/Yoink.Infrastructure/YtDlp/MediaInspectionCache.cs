using Yoink.Domain.Media;
using Yoink.Shared;

namespace Yoink.Infrastructure.Ytdlp;

/// <summary>Short-lived metadata cache. Concurrent lookups reuse the first successful result.</summary>
internal sealed class MediaInspectionCache
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<(string Url, string Proxy, string Cookies), (MediaMetadata Media, DateTimeOffset Expires)> _entries = new();
    private readonly TimeProvider _clock;

    public MediaInspectionCache(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    public async Task<Result<MediaMetadata>> GetAsync(
        (string Url, string Proxy, string Cookies) key,
        Func<CancellationToken, Task<Result<MediaMetadata>>> inspect,
        bool forceRefresh, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && _entries.TryGetValue(key, out var entry) && entry.Expires > _clock.GetUtcNow())
                return Result<MediaMetadata>.Success(entry.Media);

            // An explicit refresh must not resurrect old data if the fresh lookup fails.
            _entries.Remove(key);
            var result = await inspect(ct).ConfigureAwait(false);
            if (result.IsSuccess && result.Value is { IsLive: false } media && media.Formats.Any(f => f.HasVideo || f.HasAudio))
            {
                foreach (var expired in _entries.Where(e => e.Value.Expires <= _clock.GetUtcNow()).Select(e => e.Key).ToList())
                    _entries.Remove(expired);
                if (_entries.Count >= 32)
                    _entries.Remove(_entries.MinBy(e => e.Value.Expires).Key);
                _entries[key] = (media, _clock.GetUtcNow().AddMinutes(2));
            }
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }
}
