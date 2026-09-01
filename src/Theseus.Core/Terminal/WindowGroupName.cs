using Theseus.Core.Domain;

namespace Theseus.Core.Terminal;

/// <summary>
/// Which named terminal window a session should start in.
///
/// The name is what <c>wt -w</c> is given, and it is the whole of grouping: windows with the same
/// name are the same window, and a name nothing is using yet makes a new one. Nothing else needs to
/// exist for sessions to be grouped.
///
/// A workstream may name its own, for the case where the lane is not the right answer — two
/// workstreams that have to be watched side by side, say. Otherwise the lane decides, and if the
/// setting says one window then everything shares the configured name as it always has.
/// </summary>
public static class WindowGroupName
{
    /// <summary>The window name for one workstream at the moment it is started.</summary>
    /// <param name="grouping">Whether lanes get windows of their own.</param>
    /// <param name="configured">The single window name, used when they do not.</param>
    /// <param name="workstream">The workstream being started, for its lane and any override.</param>
    public static string For(WindowGrouping grouping, string? configured, Workstream workstream)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        var fallback = string.IsNullOrWhiteSpace(configured) ? "theseus" : configured.Trim();

        if (!string.IsNullOrWhiteSpace(workstream.WindowGroup))
        {
            return Sanitise(workstream.WindowGroup!) is { Length: > 0 } chosen ? chosen : fallback;
        }

        if (grouping is not WindowGrouping.WindowPerLane)
        {
            return fallback;
        }

        return Sanitise(workstream.Column) is { Length: > 0 } lane ? $"{fallback}-{lane}" : fallback;
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
