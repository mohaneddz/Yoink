using Yoink.Domain.Media;
using Yoink.Shared;

namespace Yoink.Infrastructure.Ytdlp;

/// <summary>Bounded metadata cache. Only requests for the same source share an inspection.</summary>
internal sealed class MediaInspectionCache
{
    private readonly object _sync = new();
    private readonly Dictionary<(string Url, string Proxy, string Cookies), (MediaMetadata Media, DateTimeOffset Expires)> _entries = new();
    private readonly Dictionary<(string Url, string Proxy, string Cookies), Task<Result<MediaMetadata>>> _pending = new();
    private readonly TimeProvider _clock;

    public MediaInspectionCache(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    public Task<Result<MediaMetadata>> GetAsync(
        (string Url, string Proxy, string Cookies) key,
        Func<CancellationToken, Task<Result<MediaMetadata>>> inspect,
        bool forceRefresh, CancellationToken ct, int cacheMinutes = 2)
    {
        ct.ThrowIfCancellationRequested();
        Task<Result<MediaMetadata>> task;
        TaskCompletionSource<Result<MediaMetadata>>? owner = null;
        lock (_sync)
        {
            if (!forceRefresh && cacheMinutes > 0 && _entries.TryGetValue(key, out var entry)
                && entry.Expires > _clock.GetUtcNow())
                return Task.FromResult(Result<MediaMetadata>.Success(entry.Media));

            if (!forceRefresh && _pending.TryGetValue(key, out task!))
                return task.WaitAsync(ct);

            // A refresh supersedes an older lookup even if it is still in flight.
            _entries.Remove(key);
            owner = new(TaskCreationOptions.RunContinuationsAsynchronously);
            task = owner.Task;
            _pending[key] = task;
        }

        _ = CompleteAsync();
        return task.WaitAsync(ct);

        async Task CompleteAsync()
        {
            using var cancellation = ct.Register(RemovePending);
            try
            {
                var result = await inspect(ct).ConfigureAwait(false);
                lock (_sync)
                {
                    if (_pending.TryGetValue(key, out var current) && ReferenceEquals(current, task))
                    {
                        _pending.Remove(key);
                        if (cacheMinutes > 0 && result.IsSuccess && result.Value is { IsLive: false } media
                            && media.Formats.Any(f => f.HasVideo || f.HasAudio))
                        {
                            var now = _clock.GetUtcNow();
                            foreach (var expired in _entries.Where(e => e.Value.Expires <= now).Select(e => e.Key).ToList())
                                _entries.Remove(expired);
                            if (_entries.Count >= 32)
                                _entries.Remove(_entries.MinBy(e => e.Value.Expires).Key);
                            _entries[key] = (media, now.AddMinutes(Math.Clamp(cacheMinutes, 1, 10)));
                        }
                    }
                }
                owner!.TrySetResult(result);
            }
            catch (OperationCanceledException ex)
            {
                RemovePending();
                owner!.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                RemovePending();
                owner!.TrySetException(ex);
            }
        }

        void RemovePending()
        {
            lock (_sync)
                if (_pending.TryGetValue(key, out var current) && ReferenceEquals(current, task))
                    _pending.Remove(key);
        }
    }
}
