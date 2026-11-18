namespace Theseus.Core.Domain;

/// <summary>
/// Where a git repository starts, from the file system alone and without running git: the directory holding its
/// <c>.git</c>, which is a folder in a clone and a file in a worktree.
/// </summary>
public static class GitRepository
{
    /// <summary>The repository root at or above <paramref name="path"/>, or null when it is not inside one.</summary>
    public static string? RootOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            for (var current = new DirectoryInfo(Path.GetFullPath(path)); current is not null; current = current.Parent)
            {
                if (!current.Exists)
                {
                    return null;
                }

                if (IsRoot(current.FullName))
                {
                    return current.FullName;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
                                              or NotSupportedException)
        {
            return null;
        }

        return null;
    }

    /// <summary>Whether this directory itself holds a <c>.git</c> folder or file.</summary>
    public static bool IsRoot(string directory)
    {
        var marker = Path.Combine(directory, ".git");

        return Directory.Exists(marker) || File.Exists(marker);
    }
}
