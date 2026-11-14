using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Yoink.Presentation.Services;

public sealed record AppReleaseInfo(string Message, string? ReleaseUrl, bool UpdateAvailable);

/// <summary>Checks published stable releases without downloading or executing an installer.</summary>
public static class AppReleaseChecker
{
    private const string LatestReleaseApi = "https://api.github.com/repos/mohaneddz/Yoink/releases/latest";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(12) };

    public static async Task<AppReleaseInfo> CheckAsync(Version currentVersion, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Yoink", currentVersion.ToString()));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = await Client.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new AppReleaseInfo("No published Yoink release yet.", null, false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        var url = root.GetProperty("html_url").GetString();
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest))
            return new AppReleaseInfo($"Latest release tag is {tag}. Open GitHub to compare versions.", ValidReleaseUrl(url), false);

        var available = latest > currentVersion;
        return new AppReleaseInfo(available
            ? $"Yoink {latest} is available. Open its release page to download it."
            : $"Yoink is up to date ({currentVersion}).",
            ValidReleaseUrl(url), available);
    }

    private static string? ValidReleaseUrl(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
        && uri.AbsolutePath.StartsWith("/mohaneddz/Yoink/releases/", StringComparison.OrdinalIgnoreCase)
            ? url : null;
}
