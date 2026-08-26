namespace Theseus.Core.Domain;

/// <summary>Turns a Jira key into the address of the issue it names.</summary>
public static class JiraSite
{
    /// <summary>
    /// The browse address for an issue, or null when there is no configured Jira instance or the
    /// text is not an issue key. An unconfigured instance is the ordinary case for anyone who does
    /// not use Jira, so the keys stay on the card and only the link goes away.
    /// </summary>
    public static string? IssueUrl(string? baseUrl, string? key)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var trimmed = key.Trim();

        if (!JiraKeyExtractor.Extract(trimmed).Contains(trimmed, StringComparer.Ordinal))
        {
            return null;
        }

        if (!Uri.TryCreate(baseUrl.Trim().TrimEnd('/'), UriKind.Absolute, out var root)
            || root.Scheme is not ("http" or "https"))
        {
            return null;
        }

        return $"{root.AbsoluteUri.TrimEnd('/')}/browse/{trimmed}";
    }
}
