namespace Theseus.Core.Domain;

/// <summary>
/// Works out which pull requests a workstream refers to and where they live. The repository is
/// found from the workstream's own directories: the working directory first, then the extra
/// directories, which is where feature worktrees are named.
/// </summary>
public sealed class PullRequestResolver
{
    private readonly IGitRemoteReader _remotes;

    /// <summary>Creates the resolver.</summary>
    public PullRequestResolver(IGitRemoteReader remotes) => _remotes = remotes;

    /// <summary>The pull requests this workstream mentions.</summary>
    public IReadOnlyList<PullRequestLink> Resolve(Workstream workstream)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        var numbers = PullRequestExtractor.Extract(workstream.Prompt);

        if (numbers.Count == 0)
        {
            return [];
        }

        var remote = Candidates(workstream)
            .Select(_remotes.ReadOriginUrl)
            .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));

        return [.. numbers.Select(number => new PullRequestLink(number, PullRequestSite.Url(remote, number)))];
    }

    private static IEnumerable<string> Candidates(Workstream workstream)
    {
        if (!string.IsNullOrWhiteSpace(workstream.WorkingDirectory))
        {
            yield return workstream.WorkingDirectory;
        }

        foreach (var directory in workstream.AddDirectories.Where(d => !string.IsNullOrWhiteSpace(d)))
        {
            yield return directory;
        }
    }
}
