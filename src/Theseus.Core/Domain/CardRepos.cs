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

        foreach (var directory in addDirectories.Where(directory => !string.IsNullOrWhiteSpace(directory) && !IsNetwork(directory)))
        {
            foreach (var root in RootsFrom(directory))
            {
                if (seen.Add(RealLocation(root)))
                {
                    found.Add(root);
                }
            }
        }

        return found;
    }

    /// <summary>
    /// A folder on another machine is skipped without being touched: the board reads repos on every refresh, and an
    /// unreachable server stalled one for 2.5 s in verification (card-branches pass 2, F2).
    /// </summary>
    private static bool IsNetwork(string directory) =>
        directory.StartsWith(@"\\", StringComparison.Ordinal) || directory.StartsWith("//", StringComparison.Ordinal);

    /// <summary>Where a repository really is, so a link or junction to it does not count it twice (F3).</summary>
    private static string RealLocation(string root)
    {
        var trimmed = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        try
        {
            return new DirectoryInfo(trimmed).ResolveLinkTarget(returnFinalTarget: true)?.FullName
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                   ?? trimmed;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return trimmed;
        }
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
                ? [.. Directory.EnumerateDirectories(directory).Where(GitRepository.IsRoot).Select(Path.GetFullPath)
                    .Order(StringComparer.OrdinalIgnoreCase)]
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }
}
