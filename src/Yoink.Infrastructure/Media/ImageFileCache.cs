using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Yoink.Infrastructure.Media;

/// <summary>Bounded persistent cache shared by thumbnail and avatar requests.</summary>
public sealed class ImageFileCache
{
    private const long MaxImageBytes = 8 * 1024 * 1024;
    private const long MaxCacheBytes = 128 * 1024 * 1024;
    private readonly HttpClient _client;
    private readonly string _directory;
    private readonly SemaphoreSlim _downloads = new(6);
    private readonly ConcurrentDictionary<string, Lazy<Task<string?>>> _pending = new();
    private readonly object _trimLock = new();
    private long _cacheSize = MaxCacheBytes;

    public ImageFileCache(HttpClient client, string directory)
    {
        _client = client;
        _directory = directory;
    }

    public async Task<string?> GetAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) return null;
        var pending = _pending.GetOrAdd(url, key => new Lazy<Task<string?>>(() => LoadAsync(key)));
        try { return await pending.Value.ConfigureAwait(false); }
        finally { ((ICollection<KeyValuePair<string, Lazy<Task<string?>>>>)_pending).Remove(new(url, pending)); }
    }

    public void Invalidate(string url)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        try { File.Delete(Path.Combine(_directory, key + ".img")); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private async Task<string?> LoadAsync(string url)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        var path = Path.Combine(_directory, key + ".img");
        string? temporary = null;
        var entered = false;
        try
        {
            var existing = new FileInfo(path);
            if (existing.Exists && existing.Length > 0 && existing.Length <= MaxImageBytes
                && existing.LastWriteTimeUtc > DateTime.UtcNow.AddDays(-30)) return path;
            await _downloads.WaitAsync().ConfigureAwait(false);
            entered = true;
            using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxImageBytes) return null;
            Directory.CreateDirectory(_directory);
            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await using (var input = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false))
            await using (var output = File.Create(temporary))
            {
                var buffer = new byte[16 * 1024];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > MaxImageBytes) return null;
                    await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);
                }
                if (total == 0) return null;
            }
            File.Move(temporary, path, overwrite: true);
            Trim(new FileInfo(path).Length);
            return path;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            // Keep showing the previous thumbnail when an expired URL or offline source cannot refresh it.
            return File.Exists(path) ? path : null;
        }
        finally
        {
            if (entered) _downloads.Release();
            try { if (temporary is not null) File.Delete(temporary); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private void Trim(long addedBytes)
    {
        lock (_trimLock)
        {
            _cacheSize += addedBytes;
            if (_cacheSize <= MaxCacheBytes) return;
            var files = new DirectoryInfo(_directory).EnumerateFiles("*.img").OrderBy(f => f.LastWriteTimeUtc).ToList();
            var size = files.Sum(f => f.Length);
            foreach (var file in files)
            {
                if (size <= MaxCacheBytes) break;
                try { file.Delete(); size -= file.Length; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            _cacheSize = size;
        }
    }
}
