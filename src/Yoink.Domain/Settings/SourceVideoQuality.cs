using System;
using System.Collections.Generic;
using System.Linq;

namespace Yoink.Domain.Settings;

public static class SourceVideoQuality
{
    public static string? SourceKey(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return null;

        var host = uri.IdnHost.ToLowerInvariant().TrimEnd('.');
        if (host == "youtu.be" || host == "youtube.com" || host.EndsWith(".youtube.com", StringComparison.Ordinal))
            return "youtube.com";
        if (host == "tiktok.com" || host.EndsWith(".tiktok.com", StringComparison.Ordinal))
            return "tiktok.com";
        foreach (var prefix in new[] { "www.", "m.", "mobile." })
            if (host.StartsWith(prefix, StringComparison.Ordinal))
                return host[prefix.Length..];
        return host;
    }

    public static int? ClosestHeight(IEnumerable<int> heights, int preferred) => heights
        .Where(h => h > 0)
        .Distinct()
        .OrderBy(h => Math.Abs((long)h - preferred))
        .ThenBy(h => h)
        .Cast<int?>()
        .FirstOrDefault();
}
