namespace Theseus.Core.Domain;

/// <summary>
/// The board's lanes, in the order they are shown. They are the user's to name and to number, so a
/// card carries its lane by name and the name is matched loosely: a lane renamed from "InFlight" to
/// "In flight" is the same lane, and cards in it should not be swept somewhere else over a space.
/// </summary>
public static class BoardLanes
{
    /// <summary>The lanes a board starts with.</summary>
    public static IReadOnlyList<string> Default { get; } = ["Today", "In flight", "Parked", "Done"];

    /// <summary>
    /// The lane a card belongs to. A card whose lane has been renamed beyond recognition, or deleted,
    /// lands in the first lane rather than vanishing from a board it is still part of.
    /// </summary>
    public static string Resolve(string? stored, IReadOnlyList<string> lanes)
    {
        ArgumentNullException.ThrowIfNull(lanes);

        if (lanes.Count == 0)
        {
            return Default[0];
        }

        return lanes.FirstOrDefault(lane => Same(lane, stored)) ?? lanes[0];
    }

    /// <summary>
    /// Whether a lane is the one a whole-board start leaves alone. Starting everything is the one
    /// gesture with real cost, and the last lane is where finished work goes.
    /// </summary>
    public static bool IsLast(string? lane, IReadOnlyList<string> lanes)
    {
        ArgumentNullException.ThrowIfNull(lanes);

        return lanes.Count > 0 && Same(lanes[^1], lane);
    }

    /// <summary>
    /// The lanes as they will be used: blanks dropped, duplicates dropped, and the defaults when
    /// nothing usable is left. A board with no lanes has nowhere to put a card.
    /// </summary>
    public static IReadOnlyList<string> Sanitise(IEnumerable<string>? lanes)
    {
        if (lanes is null)
        {
            return Default;
        }

        var kept = new List<string>();

        foreach (var lane in lanes.Select(lane => lane?.Trim()).OfType<string>())
        {
            if (lane.Length > 0 && !kept.Any(existing => Same(existing, lane)))
            {
                kept.Add(lane);
            }
        }

        return kept.Count > 0 ? kept : Default;
    }

    /// <summary>Whether two lane names mean the same lane, ignoring case and punctuation.</summary>
    public static bool Same(string lane, string? other) =>
        other is not null && string.Equals(Key(lane), Key(other), StringComparison.OrdinalIgnoreCase);

    private static string Key(string lane) =>
        string.Concat(lane.Where(char.IsLetterOrDigit));
}
