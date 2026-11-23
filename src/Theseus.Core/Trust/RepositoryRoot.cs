namespace Theseus.Core.Trust;

/// <summary>
/// The main working tree of the git repository a folder is in, read from the files on disk without running git. Claude
/// Code records a folder's trust against this root, so one record covers the repository and every worktree of it
/// (its trust dialog: "Trusting it trusts that whole repository, including its other worktrees").
/// </summary>
public static class RepositoryRoot
{
    /// <summary>The folder holding the nearest <c>.git</c> at or above <paramref name="folder"/>, or null.</summary>
    public static string? WorkingTreeOf(string folder)
    {
        for (var current = new DirectoryInfo(folder); current is not null; current = current.Parent)
        {
            var git = Path.Combine(current.FullName, ".git");

            if (Directory.Exists(git) || File.Exists(git))
            {
                return current.FullName;
            }
        }

        return null;
    }

    /// <summary>
    /// The main repository's working tree: the working tree itself for a plain clone, and for a linked worktree the
    /// folder its <c>gitdir</c> line and that folder's <c>commondir</c> lead back to. Null when not in a repository.
    /// </summary>
    public static string? MainOf(string folder)
    {
        if (WorkingTreeOf(folder) is not { } tree)
        {
            return null;
        }

        var git = Path.Combine(tree, ".git");

        if (Directory.Exists(git))
        {
            return tree;
        }

        try
        {
            var line = File.ReadLines(git).FirstOrDefault(text => text.StartsWith("gitdir:", StringComparison.Ordinal));

            if (line is null)
            {
                return tree;
            }

            var gitDir = Path.GetFullPath(line["gitdir:".Length..].Trim(), tree);
            var commonFile = Path.Combine(gitDir, "commondir");

            // A submodule's .git file has no commondir: it is its own repository.
            if (!File.Exists(commonFile))
            {
                return tree;
            }

            var commonDir = Path.GetFullPath(File.ReadAllText(commonFile).Trim(), gitDir);

            // A worktree of a bare repository has no main working tree to key it by: it is its own.
            if (!string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(commonDir)), ".git", StringComparison.OrdinalIgnoreCase))
            {
                return tree;
            }

            return Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(commonDir)) ?? tree;
        }
        catch (IOException)
        {
            return tree;
        }
        catch (UnauthorizedAccessException)
        {
            return tree;
        }
    }
}
