namespace Yoink.Application.Abstractions;

/// <summary>
/// Resolves a SOCKS proxy URL for media URLs that belong to ISP-blocked domains.
/// Returns <c>null</c> when no proxy is needed (i.e. the domain is not blocked).
/// </summary>
public interface IProxyResolver
{
    /// <summary>
    /// Check whether <paramref name="url"/> targets a blocked domain and, if so,
    /// return a ready-to-use SOCKS5 proxy address (e.g. <c>socks5://127.0.0.1:9150</c>).
    /// </summary>
    Task<string?> ResolveAsync(string url, CancellationToken ct = default);
}
