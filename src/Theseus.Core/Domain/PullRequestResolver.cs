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
    /// <param name="workstream">The card.</param>
    /// <param name="repos">
    /// The card's repos if already found this refresh (<see cref="CardRepos.Find"/>), so they are not looked for twice.
    /// </param>
    public IReadOnlyList<PullRequestLink> Resolve(Workstream workstream, IReadOnlyList<string>? repos = null)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        var numbers = PullRequestExtractor.Extract(workstream.Prompt);

        if (numbers.Count == 0)
        {
            return [];
        }

        repos ??= CardRepos.Find(workstream.AddDirectories);

        if (repos.Count > 0)
        {
            // One origin written two ways (".git" or not, a trailing slash, ssh or https) is one origin (pass 2, F5).
            var origins = repos
                .Select(_remotes.ReadOriginUrl)
                .OfType<string>()
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .DistinctBy(OriginKey)
                .ToList();

            // A number in a prompt does not say which repository it is in. With one origin between the card's repos
            // there is only one place it can be; with several, a guessed link could open the wrong pull request.
            if (origins.Count > 1)
            {
                var names = string.Join(", ", repos.Select(repo => new DirectoryInfo(repo).Name));

                return [.. numbers.Select(number => new PullRequestLink(number, null) { Note = $"in one of {names}" })];
            }

            if (origins.Count == 1)
            {
                return [.. numbers.Select(number => new PullRequestLink(number, PullRequestSite.Url(origins[0], number)))];
            }

            // Declared repos replace the working directory (spec AC 4), so with none of them able to say where its
            // pull requests live, the working directory is not asked either: that would be the guessed link AC 11
            // rules out (review should-fix 1, 2026-11-19).
            var unlinked = string.Join(", ", repos.Select(repo => new DirectoryInfo(repo).Name));

            return [.. numbers.Select(number => new PullRequestLink(number, null) { Note = $"{unlinked} have no remote to link to" })];
        }

        var remote = Candidates(workstream)
            .Select(_remotes.ReadOriginUrl)
            .FirstOrDefault(url => !string.IsNullOrWhiteSpace(url));

        return [.. numbers.Select(number => new PullRequestLink(number, PullRequestSite.Url(remote, number)))];
    }

    /// <summary>An origin reduced to host and path, lower case, without scheme, user, port separator or ".git".</summary>
    private static string OriginKey(string url)
    {
        var key = url.Trim().ToLowerInvariant();
        var scheme = key.IndexOf("://", StringComparison.Ordinal);

        if (scheme >= 0)
        {
            key = key[(scheme + 3)..];
        }
        else if (key.IndexOf(':') is var colon and > 0 && !key[..colon].Contains('/'))
        {
            key = $"{key[..colon]}/{key[(colon + 1)..]}";
        }

        if (key.IndexOf('@') is var at and >= 0 && at < key.IndexOf('/'))
        {
            key = key[(at + 1)..];
        }

        key = key.TrimEnd('/');

        return key.EndsWith(".git", StringComparison.Ordinal) ? key[..^4] : key;
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
