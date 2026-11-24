namespace Theseus.Core.CiStatus;

/// <summary>A repository on github.com, as owner and name, read from a git origin remote.</summary>
/// <param name="Owner">The account or organisation.</param>
/// <param name="Name">The repository's name, without ".git".</param>
public sealed record GitHubRepoRef(string Owner, string Name)
{
    /// <summary>"owner/name", as GitHub writes it.</summary>
    public string FullName => $"{Owner}/{Name}";

    /// <summary>The same repository however its name is cased, so two cards' remotes poll it once.</summary>
    public string Key => FullName.ToLowerInvariant();

    /// <summary>The repository an origin remote names, or null when it is not on github.com.</summary>
    public static GitHubRepoRef? FromRemote(string? remoteUrl)
    {
        if (RemoteHost.Split(remoteUrl) is not var (host, path)
            || !string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
        {
            return null;
        }

        var name = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[1][..^4] : parts[1];

        return name.Length == 0 ? null : new GitHubRepoRef(parts[0], name);
    }
}
