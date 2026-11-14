using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Yoink.Infrastructure.Proxy;

/// <summary>
/// Manages a private, headless Tor daemon that lives inside <c>%LOCALAPPDATA%\Yoink\tools\tor\</c>.
/// Downloads the official Tor Expert Bundle on first use, writes a minimal <c>torrc</c>, and
/// starts/stops the process on demand. The SOCKS port is <see cref="SocksPort"/> (9152) so it
/// never conflicts with a Tor Browser instance on 9150.
/// </summary>
public sealed class TorManager : IDisposable
{
    private const string TorExpertBundleUrl =
        "https://archive.torproject.org/tor-package-archive/torbrowser/16.0/tor-expert-bundle-windows-x86_64-16.0.tar.gz";

    /// <summary>Port chosen to avoid collisions with Tor Browser (9150) or a system Tor (9050).</summary>
    public const int SocksPort = 9152;

    private readonly string _torDir;       // .../tools/tor
    private readonly string _torExePath;   // .../tools/tor/tor/tor.exe
    private readonly string _torrcPath;    // .../tools/tor/torrc
    private readonly string _stateDir;     // .../tools/tor/state (Tor runtime data)
    private readonly string _dataDir;      // .../tools/tor/data  (geoip files from the bundle)
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<TorManager> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Process? _torProcess;

    public TorManager(IHttpClientFactory httpFactory, ILogger<TorManager> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;

        _torDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Yoink", "tools", "tor");
        _torExePath = Path.Combine(_torDir, "tor", "tor.exe");
        _torrcPath = Path.Combine(_torDir, "torrc");
        _stateDir = Path.Combine(_torDir, "state");
        _dataDir = Path.Combine(_torDir, "data");

        // Best-effort cleanup on process exit (covers hard kills / ALT-F4)
        AppDomain.CurrentDomain.ProcessExit += (_, _) => StopTor();
    }

    /// <summary>Whether our managed <c>tor.exe</c> process is still alive.</summary>
    public bool IsRunning => _torProcess is { HasExited: false };

    /// <summary>Whether <c>tor.exe</c> has already been downloaded to <c>tools/tor/</c>.</summary>
    public bool IsProvisioned => File.Exists(_torExePath);

    // ──────────────────────────────────────────────────────────────────────
    //  Public API
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Guarantee a Tor SOCKS proxy is listening on <see cref="SocksPort"/>.
    /// Downloads the expert bundle on first use, then starts the daemon and waits for bootstrap.
    /// </summary>
    public async Task<bool> EnsureRunningAsync(IProgress<string>? log, CancellationToken ct)
    {
        if (IsRunning)
            return true;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Re-check after acquiring the lock
            if (IsRunning)
                return true;

            // A leftover tor.exe (or another instance) may already hold the port
            if (await IsPortOpenAsync(SocksPort, ct).ConfigureAwait(false))
            {
                _logger.LogInformation("Managed Tor port {Port} already in use — reusing", SocksPort);
                return true;
            }

            // Provision if first run
            if (!IsProvisioned)
            {
                log?.Report("Downloading Tor relay (one-time setup)…");
                await ProvisionAsync(log, ct).ConfigureAwait(false);
            }

            // Write/refresh torrc
            WriteTorrc();

            // Start and wait for bootstrap
            log?.Report("Starting proxy tunnel…");
            return await StartTorAsync(log, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        StopTor();
        _gate.Dispose();
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Provisioning — download + extract the Tor Expert Bundle
    // ──────────────────────────────────────────────────────────────────────

    private async Task ProvisionAsync(IProgress<string>? log, CancellationToken ct)
    {
        Directory.CreateDirectory(_torDir);

        var archivePath = Path.Combine(_torDir, "tor-expert-bundle.tar.gz");

        // Download
        var client = _httpFactory.CreateClient("tools");
        using var response = await client.GetAsync(TorExpertBundleUrl,
            HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        var tmp = archivePath + ".part";

        await using (var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var output = File.Create(tmp))
        {
            var buffer = new byte[81920];
            long read = 0;
            int lastPercent = -1;
            int n;
            while ((n = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                read += n;
                if (total > 0)
                {
                    var percent = (int)(read * 100 / total);
                    if (percent != lastPercent && percent % 10 == 0)
                    {
                        lastPercent = percent;
                        log?.Report($"  {percent}% ({read / 1_048_576} / {total / 1_048_576} MB)");
                    }
                }
            }
        }

        if (File.Exists(archivePath)) File.Delete(archivePath);
        File.Move(tmp, archivePath);

        // Extract .tar.gz
        log?.Report("Extracting Tor…");
        await ExtractTarGzAsync(archivePath, _torDir, ct).ConfigureAwait(false);

        try { File.Delete(archivePath); } catch { /* best effort */ }
        _logger.LogInformation("Tor Expert Bundle provisioned to {Dir}", _torDir);
    }

    private static async Task ExtractTarGzAsync(string archivePath, string destinationDir, CancellationToken ct)
    {
        await using var fileStream = File.OpenRead(archivePath);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        await using var tarReader = new TarReader(gzipStream);

        while (await tarReader.GetNextEntryAsync(cancellationToken: ct).ConfigureAwait(false) is { } entry)
        {
            // Only extract files (skip directories — they are created implicitly)
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                continue;

            var entryName = entry.Name.Replace('/', Path.DirectorySeparatorChar);

            // Safety: skip entries that try to escape the destination
            if (entryName.Contains("..", StringComparison.Ordinal))
                continue;

            var destPath = Path.Combine(destinationDir, entryName);
            var destDir = Path.GetDirectoryName(destPath);
            if (destDir is not null) Directory.CreateDirectory(destDir);

            await entry.ExtractToFileAsync(destPath, overwrite: true, ct).ConfigureAwait(false);
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Torrc
    // ──────────────────────────────────────────────────────────────────────

    private void WriteTorrc()
    {
        Directory.CreateDirectory(_stateDir);

        var geoip = Path.Combine(_dataDir, "geoip");
        var geoip6 = Path.Combine(_dataDir, "geoip6");

        var lines = new List<string>
        {
            $"SocksPort {SocksPort}",
            $"DataDirectory {_stateDir}",
            "Log notice stderr"
        };

        if (File.Exists(geoip))  lines.Add($"GeoIPFile {geoip}");
        if (File.Exists(geoip6)) lines.Add($"GeoIPv6File {geoip6}");

        File.WriteAllLines(_torrcPath, lines);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Process lifecycle
    // ──────────────────────────────────────────────────────────────────────

    private async Task<bool> StartTorAsync(IProgress<string>? log, CancellationToken ct)
    {
        StopTor(); // kill any leftover

        var bootstrapDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        _torProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _torExePath,
                ArgumentList = { "-f", _torrcPath },
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(_torExePath)!
            },
            EnableRaisingEvents = true
        };

        _torProcess.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            _logger.LogDebug("[tor] {Line}", e.Data);

            if (e.Data.Contains("Bootstrapped 100%", StringComparison.OrdinalIgnoreCase))
            {
                log?.Report("Proxy tunnel ready.");
                bootstrapDone.TrySetResult(true);
            }
            else if (e.Data.Contains("Bootstrapped", StringComparison.OrdinalIgnoreCase))
            {
                // Forward bootstrap progress (e.g. "Bootstrapped 50% (loading_descriptors)")
                log?.Report(e.Data.TrimStart('[').TrimStart());
            }
        };

        _torProcess.Exited += (_, _) =>
        {
            _logger.LogWarning("Managed Tor process exited with code {Code}",
                _torProcess?.ExitCode ?? -1);
            bootstrapDone.TrySetResult(false);
        };

        if (!_torProcess.Start())
        {
            _logger.LogError("Failed to start tor.exe at {Path}", _torExePath);
            return false;
        }

        _torProcess.BeginErrorReadLine();
        _torProcess.BeginOutputReadLine();

        // Wait up to 90 seconds for bootstrap
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));

        try
        {
            using var _ = timeout.Token.Register(() => bootstrapDone.TrySetResult(false));
            var result = await bootstrapDone.Task.ConfigureAwait(false);

            if (!result)
            {
                _logger.LogWarning("Tor failed to bootstrap within the timeout");
                StopTor();
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            StopTor();
            throw;
        }
    }

    private void StopTor()
    {
        try
        {
            if (_torProcess is { HasExited: false })
            {
                _torProcess.Kill(entireProcessTree: true);
                _torProcess.Dispose();
            }
        }
        catch
        {
            // Already exited or access denied — nothing to do
        }
        finally
        {
            _torProcess = null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────────────────

    internal static async Task<bool> IsPortOpenAsync(int port, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMilliseconds(500));
            await client.ConnectAsync(IPAddress.Loopback, port, cts.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
