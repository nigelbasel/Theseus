namespace Theseus.Core.Terminal;

/// <summary>
/// Chooses between the terminal executables available on this machine. Two Windows Terminal
/// packages can be installed at once and both claim the <c>wt.exe</c> alias, so the alias is the
/// last resort: the executable already hosting the user's windows is the one whose named windows
/// a new tab can actually join.
/// </summary>
public static class TerminalSelector
{
    /// <summary>Picks a terminal from what is running, falling back to the execution alias.</summary>
    /// <param name="runningExecutablePaths">The image path of every running terminal process.</param>
    /// <param name="aliasPath">The <c>wt.exe</c> alias, if it is present on disk.</param>
    public static TerminalResolution Choose(IReadOnlyList<string> runningExecutablePaths, string? aliasPath)
    {
        ArgumentNullException.ThrowIfNull(runningExecutablePaths);

        var groups = runningExecutablePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Path = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => group.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (groups.Count > 0)
        {
            return new TerminalResolution(groups[0].Path, TerminalSource.RunningInstance, groups.Count);
        }

        if (!string.IsNullOrWhiteSpace(aliasPath))
        {
            return new TerminalResolution(aliasPath, TerminalSource.ExecutionAlias, 0);
        }

        return new TerminalResolution(string.Empty, TerminalSource.None, 0);
    }
}
