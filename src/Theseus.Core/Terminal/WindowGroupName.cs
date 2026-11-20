using Theseus.Core.Domain;

namespace Theseus.Core.Terminal;

/// <summary>
/// Which named terminal window a session should start in.
///
/// The name is what <c>wt -w</c> is given, and it is the whole of grouping: windows with the same
/// name are the same window, and a name nothing is using yet makes a new one. Nothing else needs to
/// exist for sessions to be grouped.
///
/// The lane decides, because the lanes are already the board's own grouping of the work and the
/// screen may as well mirror it. A workstream may name its own window instead, for the case where
/// the lane is not the right answer — two workstreams that have to be watched side by side, say.
/// </summary>
public static class WindowGroupName
{
    /// <summary>The window name for one workstream at the moment it is started.</summary>
    /// <param name="configured">The base name, which every window is named after.</param>
    /// <param name="workstream">The workstream being started, for its lane and any override.</param>
    /// <param name="areaWindows">Areas whose window keeps an earlier name, from the settings.</param>
    /// <param name="readableProjects">
    /// The projects that read now. A card in one of them opens in its project's window; with none, every card gets the
    /// name it always did (project-view AC 22).
    /// </param>
    public static string For(string? configured, Workstream workstream,
        IReadOnlyDictionary<string, string>? areaWindows = null, IReadOnlySet<string>? readableProjects = null)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        var fallback = string.IsNullOrWhiteSpace(configured) ? "theseus" : configured.Trim();

        if (!string.IsNullOrWhiteSpace(workstream.WindowGroup))
        {
            return Sanitise(workstream.WindowGroup!) is { Length: > 0 } chosen ? chosen : fallback;
        }

        // A project's cards share one window, after a card's own override and before its area (project-view AC 19).
        // "-project-" keeps it apart from an area of the same name. An unreadable project's card is placed as before,
        // as it sits on the grid as before (AC 13).
        if (workstream.Project is { } project && readableProjects?.Contains(project) == true
            && ForProject(configured, project) is { } projectWindow)
        {
            return projectWindow;
        }

        // The area names the window, not the stage. A window called after a stage would want
        // renaming every time work advanced, and a session cannot move between windows once it has
        // started; the kind of work is the part that stays put. A board with no areas yet falls back
        // to its lane, which is what it used to do.
        var kept = areaWindows?
            .FirstOrDefault(pair => string.Equals(pair.Key, workstream.Area?.Trim(), StringComparison.OrdinalIgnoreCase))
            .Value;
        var row = Sanitise(kept) is { Length: > 0 } window ? window
            : Sanitise(workstream.Area) is { Length: > 0 } area ? area
            : Sanitise(workstream.Column);

        return row.Length > 0 ? $"{fallback}-{row}" : fallback;
    }

    /// <summary>A project's window, <c>&lt;base&gt;-project-&lt;project&gt;</c>, or null for a name that sanitises to nothing.</summary>
    public static string? ForProject(string? configured, string project)
    {
        var fallback = string.IsNullOrWhiteSpace(configured) ? "theseus" : configured.Trim();

        return Sanitise(project) is { Length: > 0 } named ? $"{fallback}-project-{named}" : null;
    }

    /// <summary>
    /// A name reduced to what is safe to pass and easy to read: lower case, words joined by hyphens,
    /// nothing else. Lane names are typed by hand and hold spaces, slashes and worse, and the name
    /// ends up on a command line and in a log.
    /// </summary>
    public static string Sanitise(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var cleaned = new string([.. name.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')]);

        while (cleaned.Contains("--", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        }

        return cleaned.Trim('-');
    }
}
