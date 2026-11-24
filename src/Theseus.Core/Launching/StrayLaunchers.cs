namespace Theseus.Core.Launching;

/// <summary>
/// The launchers Theseus clears up at start-up: its own, run from an earlier release's folder under its launcher folder,
/// with no session left and older than the longest a tab is held. Those releases held a failed tab until Enter was
/// pressed; this release's launchers end on their own. Every launcher with a session running is a live tab and is left
/// alone, whichever release it came from.
/// </summary>
public static class StrayLaunchers
{
    /// <summary>The launchers among <paramref name="running"/> that have outlived their session.</summary>
    /// <param name="running">The launchers running now.</param>
    /// <param name="launcherRoot">The folder Theseus copies its launchers into, one subfolder per release.</param>
    /// <param name="currentKit">This release's subfolder, whose launchers are left to end on their own.</param>
    /// <param name="now">The time now.</param>
    public static IReadOnlyList<LauncherProcess> Among(IEnumerable<LauncherProcess> running, string launcherRoot, string currentKit,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(running);
        ArgumentException.ThrowIfNullOrWhiteSpace(launcherRoot);

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(launcherRoot)) + Path.DirectorySeparatorChar;
        var current = Path.TrimEndingDirectorySeparator(Path.GetFullPath(currentKit)) + Path.DirectorySeparatorChar;

        return
        [
            .. running.Where(launcher => !launcher.HasChild
                                         && now - launcher.StartedUtc > LauncherHold.Limit
                                         && Path.GetFullPath(launcher.ImagePath).StartsWith(root, StringComparison.OrdinalIgnoreCase)
                                         && !Path.GetFullPath(launcher.ImagePath).StartsWith(current, StringComparison.OrdinalIgnoreCase)),
        ];
    }
}
