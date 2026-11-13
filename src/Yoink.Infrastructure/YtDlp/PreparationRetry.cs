namespace Yoink.Infrastructure.Ytdlp;

internal static class PreparationRetry
{
    internal static bool IsTransient(string? error)
    {
        if (string.IsNullOrWhiteSpace(error)) return false;
        string[] permanent = ["HTTP Error 403", "HTTP Error 429", "verification", "sign in", "login", "cookies", "Requested format", "Permission denied", "No space left"];
        if (permanent.Any(s => error.Contains(s, StringComparison.OrdinalIgnoreCase))) return false;
        string[] transient = ["curl: (56)", "curl: (52)", "curl: (28)", "curl: (18)", "connection reset", "connection closed abruptly", "remote end closed connection", "connection timed out", "read timed out"];
        return transient.Any(s => error.Contains(s, StringComparison.OrdinalIgnoreCase));
    }

    internal static async Task<T> RunAsync<T>(Func<Task<T>> operation, Func<T, bool> shouldRetry,
        CancellationToken ct, Action<int>? onRetry = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        delay ??= Task.Delay;
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var result = await operation().ConfigureAwait(false);
            if (attempt >= 3 || !shouldRetry(result)) return result;
            ct.ThrowIfCancellationRequested();
            onRetry?.Invoke(attempt + 1);
            await delay(TimeSpan.FromSeconds(attempt), ct).ConfigureAwait(false);
        }
    }
}
