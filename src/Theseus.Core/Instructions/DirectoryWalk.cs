using Theseus.Core.Diagnostics;

namespace Theseus.Core.Instructions;

/// <summary>
/// The file system reads the instruction and skill scans are built on, each one safe to point at a
/// directory that is missing, locked or enormous. A working directory like <c>C:\dev\tfsgit</c>
/// holds eighty repositories, so the searches below it are bounded and skip build output.
/// </summary>
public static class DirectoryWalk
{
    /// <summary>Claude Code skips a CLAUDE.md larger than this, so there is nothing in one to read.</summary>
    public const long LargestReadable = 4L * 1024 * 1024;

    private static readonly string[] UninterestingDirectories = ["node_modules", "bin", "obj", "dist", "packages"];

    /// <summary>A directory and every directory above it, the filesystem root first.</summary>
    public static IReadOnlyList<string> RootDownTo(string directory)
    {
        var chain = new List<string>();

        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            chain.Add(current.FullName);
        }

        chain.Reverse();

        return chain;
    }

    /// <summary>
    /// The directories below a root, a level at a time, down to <paramref name="depth"/> levels. Hidden
    /// directories, build output and junctions are not entered.
    /// </summary>
    public static IEnumerable<(string Directory, string Relative)> Below(string root, int depth)
    {
        var frontier = new List<string> { root };

        for (var level = 0; level < depth && frontier.Count > 0; level++)
        {
            var next = frontier.SelectMany(Children).Where(IsWorthSearching).ToList();

            foreach (var directory in next)
            {
                yield return (directory, Path.GetRelativePath(root, directory));
            }

            frontier = next;
        }
    }

    /// <summary>The directories directly inside one, in name order, or none when it cannot be listed.</summary>
    public static IReadOnlyList<string> Children(string directory) =>
        Listed(directory, () => Directory.EnumerateDirectories(directory));

    /// <summary>The files in a directory matching a pattern, in name order, or none when it cannot be listed.</summary>
    public static IReadOnlyList<string> Files(string directory, string pattern, bool recursive) =>
        Listed(directory, () => Directory.EnumerateFiles(directory, pattern,
            recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly));

    /// <summary>A file's text, or nothing when it is missing, unreadable or too large for Claude Code to load.</summary>
    public static string Text(string path)
    {
        try
        {
            var file = new FileInfo(path);

            return file.Exists && file.Length <= LargestReadable ? File.ReadAllText(path) : string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("Could not read {Path}: {Reason}", path, exception.Message);

            return string.Empty;
        }
    }

    private static IReadOnlyList<string> Listed(string directory, Func<IEnumerable<string>> list)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        try
        {
            return [.. list().Order(StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("Could not list {Directory}: {Reason}", directory, exception.Message);

            return [];
        }
    }

    private static bool IsWorthSearching(string directory)
    {
        var name = Path.GetFileName(directory);

        if (name.StartsWith('.') || UninterestingDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            return (File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
