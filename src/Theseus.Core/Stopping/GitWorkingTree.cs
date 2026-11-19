namespace Theseus.Core.Stopping;

/// <summary>
/// Whether a folder lies inside a git working tree. Theseus does not create files there on its own: a later
/// <c>git add -A</c> would commit a file holding a machine path into the repository (handoff-names review S5).
/// </summary>
public static class GitWorkingTree
{
    /// <summary>Whether the folder or any parent holds a <c>.git</c> folder or file.</summary>
    public static bool Contains(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return false;
        }

        for (var directory = new DirectoryInfo(folder); directory is not null; directory = directory.Parent)
        {
            var marker = Path.Combine(directory.FullName, ".git");

            if (Directory.Exists(marker) || File.Exists(marker))
            {
                return true;
            }
        }

        return false;
    }
}
