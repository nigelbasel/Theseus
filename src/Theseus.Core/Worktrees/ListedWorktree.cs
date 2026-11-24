namespace Theseus.Core.Worktrees;

/// <summary>One entry of <c>git worktree list --porcelain</c>.</summary>
/// <param name="Folder">Its folder, full path.</param>
/// <param name="Branch">Its branch, or null when detached or bare.</param>
public sealed record ListedWorktree(string Folder, string? Branch)
{
    /// <summary>The entries of a porcelain listing, in its order.</summary>
    public static IReadOnlyList<ListedWorktree> Parse(string porcelain)
    {
        ArgumentNullException.ThrowIfNull(porcelain);

        var listed = new List<ListedWorktree>();
        string? folder = null;
        string? branch = null;

        foreach (var raw in porcelain.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            if (line.StartsWith("worktree ", StringComparison.Ordinal))
            {
                Flush();
                folder = line["worktree ".Length..];
            }
            else if (line.StartsWith("branch ", StringComparison.Ordinal))
            {
                var reference = line["branch ".Length..];
                branch = reference.StartsWith("refs/heads/", StringComparison.Ordinal) ? reference["refs/heads/".Length..] : reference;
            }
        }

        Flush();

        return listed;

        void Flush()
        {
            if (folder is not null)
            {
                listed.Add(new ListedWorktree(Normal(folder), branch));
            }

            folder = null;
            branch = null;
        }
    }

    /// <summary>A folder as compared: full, with no trailing separator.</summary>
    public static string Normal(string folder) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder.Replace('/', Path.DirectorySeparatorChar)));

    /// <summary>Whether this entry is that folder.</summary>
    public bool Is(string other) => string.Equals(Folder, Normal(other), StringComparison.OrdinalIgnoreCase);
}
