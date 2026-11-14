using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Yoink.Application.Abstractions;

namespace Yoink.Infrastructure.Proxy;

/// <summary>
/// Two-tier proxy resolver for ISP-blocked domains.
/// <list type="number">
///   <item>If Tor Browser is already running (port 9150), reuse it.</item>
///   <item>Otherwise, start a managed headless Tor daemon (port 9152) via <see cref="TorManager"/>.</item>
/// </list>
/// Returns <c>null</c> for non-blocked domains so they connect directly at full speed.
/// </summary>
public sealed class ProxyResolver : IProxyResolver
{
    /// <summary>Port used by Tor Browser's built-in SOCKS proxy.</summary>
    private const int TorBrowserPort = 9150;

    private readonly TorManager _torManager;
    private readonly ILogger<ProxyResolver> _logger;

    public ProxyResolver(TorManager torManager, ILogger<ProxyResolver> logger)
    {
        _torManager = torManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string?> ResolveAsync(string url, CancellationToken ct = default)
    {
        if (!NeedsProxy(url))
            return null;

        _logger.LogDebug("Blocked domain detected for {Url} — resolving proxy", url);

        // Tier 1: Tor Browser already open?
        if (await TorManager.IsPortOpenAsync(TorBrowserPort, ct).ConfigureAwait(false))
        {
            _logger.LogInformation("Using existing Tor Browser proxy on port {Port}", TorBrowserPort);
            return $"socks5://127.0.0.1:{TorBrowserPort}";
        }

        // Tier 2: start (or reuse) the managed headless Tor daemon
        bool started;
        try
        {
            started = await _torManager.EnsureRunningAsync(null, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Managed Tor setup failed for {Url}", url);
            started = false;
        }
        if (started)
        {
            _logger.LogInformation("Using managed Tor proxy on port {Port}", TorManager.SocksPort);
            return $"socks5://127.0.0.1:{TorManager.SocksPort}";
        }

        _logger.LogWarning("No proxy available for blocked URL: {Url}", url);
        return null;
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Domain matching
    // ──────────────────────────────────────────────────────────────────────

    private static bool NeedsProxy(string url) => Yoink.Domain.Media.SourcePrivacy.IsPrivateSource(url);
}
