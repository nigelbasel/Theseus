namespace Theseus.Core.Domain;

/// <summary>
/// The repositories a card covers, found from its extra directories (card-branches, Nigel 2026-11-18). A workstream
/// may span repos on one branch name with a worktree per repo, so a card is not one repository; the owner's
/// convention already lists a card's worktrees among its extra directories, and a session can only work where it has
/// been given access, so that list is where a card's repos are declared.
/// </summary>
public static class CardRepos
{
    /// <summary>
    /// Each repository the directories lead to, once, in the order first reached: the repository a directory is in,
    /// or, for a directory not inside one, each repository directly beneath it (a feature folder).
    /// </summary>
    public static IReadOnlyList<string> Find(IEnumerable<string> addDirectories)
    {
        ArgumentNullException.ThrowIfNull(addDirectories);

        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in addDirectories.Where(directory => !string.IsNullOrWhiteSpace(directory)))
        {
            foreach (var root in RootsFrom(directory))
            {
                if (seen.Add(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)))
                {
                    found.Add(root);
                }
            }
        }

        return found;
    }

    /// <summary>The repositories the directories lead to, each with the branch it has checked out.</summary>
    public static IReadOnlyList<CardRepo> Read(IEnumerable<string> addDirectories) =>
        [.. Find(addDirectories).Select(root => new CardRepo(root, new DirectoryInfo(root).Name, GitBranchReader.Read(root)))];

    private static IEnumerable<string> RootsFrom(string directory)
    {
        if (GitRepository.RootOf(directory) is { } root)
        {
            return [root];
        }

        try
        {
            return Directory.Exists(directory)
                ? [.. Directory.EnumerateDirectories(directory).Where(GitRepository.IsRoot).Select(Path.GetFullPath)]
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }
}
