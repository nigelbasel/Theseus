namespace Theseus.Core.CiStatus;

/// <summary>The host and path of a git remote, written as https, ssh or scp-style ("git@host:path").</summary>
public static class RemoteHost
{
    /// <summary>The host a remote is on, or null when it is not a remote address.</summary>
    public static string? Of(string? remoteUrl) => Split(remoteUrl)?.Host;

    /// <summary>The host and the path after it, without credentials or port; null when it is not a remote address.</summary>
    public static (string Host, string Path)? Split(string? remoteUrl)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
        {
            return null;
        }

        var trimmed = remoteUrl.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return uri.Scheme is "http" or "https" or "ssh" or "git" && uri.Host.Length > 0
                ? (uri.Host, Uri.UnescapeDataString(uri.AbsolutePath).Trim('/'))
                : null;
        }

        // scp-style: [user@]host:path, where the part before the colon has no slash.
        var colon = trimmed.IndexOf(':');

        if (colon <= 0 || trimmed[..colon].Contains('/'))
        {
            return null;
        }

        var host = trimmed[..colon];
        var at = host.LastIndexOf('@');

        return (at >= 0 ? host[(at + 1)..] : host, trimmed[(colon + 1)..].Trim('/'));
    }
}
