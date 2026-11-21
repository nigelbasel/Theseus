namespace Theseus.Core.Colour;

/// <summary>
/// Each project's own colour (1.1d AC 40; decision 7, 2026-11-21): taken from <see cref="ProjectColourPalette"/>, which no
/// workstream colour is in, so a project never shares a card's colour; two projects avoid each other's while the palette
/// has room. A colour once given is kept by project name, unless it is not a project colour, when it is given again.
/// </summary>
public static class ProjectColours
{
    /// <summary>
    /// The saved colours with a project colour for every project that has none, or has one from outside the palette. They
    /// are given in name order, so the result does not depend on the order the projects were read in. The same dictionary
    /// comes back when nothing changed.
    /// </summary>
    /// <param name="saved">The colours kept so far, by project name.</param>
    /// <param name="projects">The names of the projects on the board.</param>
    public static IReadOnlyDictionary<string, string> Assign(IReadOnlyDictionary<string, string> saved, IEnumerable<string> projects)
    {
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentNullException.ThrowIfNull(projects);

        var missing = projects
            .Where(name => Of(saved, name) is null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missing.Count == 0)
        {
            return saved;
        }

        var colours = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, colour) in saved)
        {
            if (ProjectColourPalette.Contains(colour))
            {
                colours[name] = colour;
            }
        }

        foreach (var name in missing)
        {
            colours[name] = LeastUsed(colours.Values);
        }

        return colours;
    }

    /// <summary>A project's saved colour, matched by name ignoring case, or null when it has none from the project palette.</summary>
    public static string? Of(IReadOnlyDictionary<string, string> saved, string name)
    {
        ArgumentNullException.ThrowIfNull(saved);

        return saved.FirstOrDefault(entry => string.Equals(entry.Key, name, StringComparison.OrdinalIgnoreCase)).Value is { } colour
               && ProjectColourPalette.Contains(colour)
            ? colour
            : null;
    }

    // The first palette colour fewest projects hold: an unused one while there is one.
    private static string LeastUsed(IEnumerable<string> taken)
    {
        var counts = taken.GroupBy(colour => colour, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return ProjectColourPalette.All.MinBy(colour => counts.GetValueOrDefault(colour))!;
    }
}
