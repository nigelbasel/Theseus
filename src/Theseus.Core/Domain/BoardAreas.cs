namespace Theseus.Core.Domain;

/// <summary>
/// The board's areas: the rows of the grid, and the kind of work a card is rather than how far along
/// it is. Named and ordered by the user, matched loosely, and resolved the same way lanes are — an
/// area renamed beyond recognition should not sweep its cards off the board.
///
/// The area is also what a session's terminal window is named after. That is the point of separating
/// it from the stage: a window called after a stage would be renamed every time work advanced, and a
/// session cannot move between windows once it has started.
/// </summary>
public static class BoardAreas
{
    /// <summary>
    /// What the first row is called when a board has areas but a card has none. Not "other": a row
    /// named for nothing becomes a permanent home for everything nobody classified, and this is
    /// meant to be renamed or emptied.
    /// </summary>
    public const string Unassigned = "Unfiled";

    /// <summary>The areas a board starts with when nothing can be derived from what it had.</summary>
    public static IReadOnlyList<string> Default { get; } = [Unassigned];

    /// <summary>
    /// The area a card belongs to. One whose area was renamed beyond recognition, or deleted, lands
    /// in the first row rather than vanishing from a board it is still part of.
    /// </summary>
    public static string Resolve(string? stored, IReadOnlyList<string> areas)
    {
        ArgumentNullException.ThrowIfNull(areas);

        if (areas.Count == 0)
        {
            return Unassigned;
        }

        return areas.FirstOrDefault(area => Same(area, stored)) ?? areas[0];
    }

    /// <summary>The areas as they will be used: blanks and duplicates dropped.</summary>
    public static IReadOnlyList<string> Sanitise(IEnumerable<string>? areas)
    {
        if (areas is null)
        {
            return Default;
        }

        var kept = new List<string>();

        foreach (var area in areas.Select(area => area?.Trim()).OfType<string>())
        {
            if (area.Length > 0 && !kept.Any(existing => Same(existing, area)))
            {
                kept.Add(area);
            }
        }

        return kept.Count > 0 ? kept : Default;
    }

    /// <summary>Whether two area names mean the same area, ignoring case and punctuation.</summary>
    public static bool Same(string area, string? other) => LooseName.Same(area, other);
}
