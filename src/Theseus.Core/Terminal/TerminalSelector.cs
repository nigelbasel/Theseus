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
    /// <param name="installed">
    /// Every terminal installed, stable first, for when nothing is running to follow. Preferred over
    /// the alias because both packages claim the alias and it opens the preview where one exists.
    /// </param>
    public static TerminalResolution Choose(
        IReadOnlyList<string> runningExecutablePaths,
        string? aliasPath,
        IReadOnlyList<string>? installed = null)
    {
        ArgumentNullException.ThrowIfNull(runningExecutablePaths);

        installed ??= [];

        var groups = runningExecutablePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Path = group.Key, Count = group.Count() })
            .OrderByDescending(group => group.Count)
            .ThenBy(group => IsPreview(group.Path))
            .ThenBy(group => group.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (groups.Count > 0)
        {
            return new TerminalResolution(groups[0].Path, TerminalSource.RunningInstance, groups.Count);
        }

        if (installed.Count > 0)
        {
            return new TerminalResolution(installed[0], TerminalSource.RunningInstance, 0);
        }

        if (!string.IsNullOrWhiteSpace(aliasPath))
        {
            return new TerminalResolution(aliasPath, TerminalSource.ExecutionAlias, 0);
        }

        return new TerminalResolution(string.Empty, TerminalSource.None, 0);
    }

    /// <summary>
    /// Whether a path is the preview package. It loses a tie: preview installs alongside the stable
    /// one rather than replacing it, so a machine running both is a machine whose owner works in the
    /// stable one and has preview open besides. Deciding this alphabetically, as it once was, hands
    /// it to preview - "TerminalPreview" sorts before "Terminal_" - which is a coin toss dressed up
    /// as a rule.
    /// </summary>
    public static bool IsPreview(string path) =>
        path.Contains("WindowsTerminalPreview", StringComparison.OrdinalIgnoreCase);
}
