namespace Theseus.Core.Domain;

/// <summary>
/// Turns a repository's origin remote and a pull request number into the address of that pull
/// request. Deriving it from the remote means there is nothing to configure and no credentials to
/// hold: the workstream's own directory already says which repository, on which host, its work
/// belongs to.
/// </summary>
public static class PullRequestSite
{
    /// <summary>The address of a pull request, or null when the host is not one we can address.</summary>
    public static string? Url(string? remoteUrl, int number)
    {
        if (number <= 0 || string.IsNullOrWhiteSpace(remoteUrl))
        {
            return null;
        }

        if (Normalise(remoteUrl) is not var (address, host))
        {
            return null;
        }

        if (host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return $"{address}/pull/{number}";
        }

        return address.Contains("/_git/", StringComparison.OrdinalIgnoreCase)
            ? $"{address}/pullrequest/{number}"
            : null;
    }

    private static (string Base, string Host)? Normalise(string remoteUrl)
    {
        var trimmed = remoteUrl.Trim();

        if (trimmed.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
        {
            var separator = trimmed.IndexOf(':');
            if (separator < 0)
            {
                return null;
            }

            var sshHost = trimmed[4..separator];
            var path = trimmed[(separator + 1)..].TrimEnd('/');

            return (Strip($"https://{sshHost}/{path}"), sshHost);
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https" or "ssh"))
        {
            return null;
        }

        var withoutCredentials = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty };
        withoutCredentials.Scheme = uri.Scheme == "ssh" ? "https" : uri.Scheme;

        if (withoutCredentials.Uri.IsDefaultPort)
        {
            withoutCredentials.Port = -1;
        }

        return (Strip(withoutCredentials.Uri.AbsoluteUri), uri.Host);
    }

    private static string Strip(string url)
    {
        var trimmed = url.TrimEnd('/');

        return trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }
}
