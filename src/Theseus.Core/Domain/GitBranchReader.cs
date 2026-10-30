namespace Theseus.Core.Domain;

/// <summary>
/// Reads the branch a working directory has checked out from the repository's own HEAD file, without
/// invoking git. A worktree's <c>.git</c> is a file pointing at its own git directory, which has its
/// own HEAD, so a feature worktree reports its feature branch.
/// </summary>
public static class GitBranchReader
{
    /// <summary>The branch checked out, the short commit when detached, or null outside a repository.</summary>
    public static string? Read(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        try
        {
            return GitDirectory(directory) is { } gitDirectory && File.Exists(Path.Combine(gitDirectory, "HEAD"))
                ? Parse(File.ReadAllText(Path.Combine(gitDirectory, "HEAD")))
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The branch named by the contents of a HEAD file.</summary>
    public static string? Parse(string head)
    {
        var text = head.Trim();

        if (text.StartsWith("ref:", StringComparison.Ordinal))
        {
            var reference = text["ref:".Length..].Trim();

            return reference.StartsWith("refs/heads/", StringComparison.Ordinal) ? reference["refs/heads/".Length..] : reference;
        }

        return text.Length >= 7 ? $"detached at {text[..7]}" : null;
    }

    private static string? GitDirectory(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var marker = Path.Combine(current.FullName, ".git");

            if (Directory.Exists(marker))
            {
                return marker;
            }

            if (File.Exists(marker))
            {
                var pointer = File.ReadAllText(marker).Trim();

                if (!pointer.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var gitDirectory = pointer["gitdir:".Length..].Trim();

                return Path.IsPathRooted(gitDirectory)
                    ? gitDirectory
                    : Path.GetFullPath(Path.Combine(current.FullName, gitDirectory));
            }
        }

        return null;
    }
}
