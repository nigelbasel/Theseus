namespace Theseus.Core.Stopping;

/// <summary>
/// Whether a folder lies inside a git working tree whose repository can be pushed somewhere. Theseus does not create
/// files there on its own: a later <c>git add -A</c> and push would publish a file holding a machine path
/// (handoff-names review S5). A repository with no remote, such as a local notes repository, publishes nothing, so
/// handover files there are moved like any other (Nigel, 2026-11-20).
/// </summary>
public static class GitWorkingTree
{
    /// <summary>
    /// Whether the nearest <c>.git</c> folder or file above the folder belongs to a repository with a remote. A
    /// repository whose configuration cannot be found or read counts as having one, the safe way round.
    /// </summary>
    public static bool ContainsPublishable(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }

        for (var directory = new DirectoryInfo(folder); directory is not null; directory = directory.Parent)
        {
            var marker = Path.Combine(directory.FullName, ".git");

            if (Directory.Exists(marker))
            {
                return HasRemote(Path.Combine(marker, "config"));
            }

            if (File.Exists(marker))
            {
                return ConfigOfWorktree(marker, directory.FullName) is not { } config || HasRemote(config);
            }
        }

        return false;
    }

    // A worktree's .git file names its own git folder, whose commondir file names the repository's shared one.
    private static string? ConfigOfWorktree(string marker, string workingTree)
    {
        try
        {
            var line = File.ReadLines(marker).FirstOrDefault(l => l.StartsWith("gitdir:", StringComparison.Ordinal));

            if (line is null)
            {
                return null;
            }

            var gitDirectory = Path.GetFullPath(line["gitdir:".Length..].Trim(), workingTree);
            var commonFile = Path.Combine(gitDirectory, "commondir");
            var common = File.Exists(commonFile)
                ? Path.GetFullPath(File.ReadAllText(commonFile).Trim(), gitDirectory)
                : gitDirectory;

            return Path.Combine(common, "config");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static bool HasRemote(string config)
    {
        try
        {
            return !File.Exists(config)
                || File.ReadLines(config).Any(l => l.TrimStart().StartsWith("[remote \"", StringComparison.Ordinal));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
