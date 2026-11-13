namespace Yoink.Domain.Media;

public static class SourcePrivacy
{
    private static readonly HashSet<string> PrivateDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "pornhub.com",
        "xvideos.com",
        "xnxx.com",
        "xhamster.com",
        "redtube.com",
        "youporn.com",
        "tube8.com",
        "spankbang.com",
        "xhamster.desi"
    };

    public static bool IsPrivateSource(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        var host = uri.Host;

        foreach (var domain in PrivateDomains)
        {
            if (host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
