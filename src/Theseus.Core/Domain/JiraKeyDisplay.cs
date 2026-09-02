namespace Theseus.Core.Domain;

/// <summary>
/// How many of a workstream's Jira issues a card shows.
///
/// A workstream routinely spans several, and one on this board spans sixteen. Rendered in full they
/// wrap to five or six rows of chips and the card grows taller than everything around it — which on
/// a grid pushes its whole row out of shape, so one busy workstream distorts the board.
///
/// The card is a glance, not a record. It shows the first few and says how many more there are; the
/// details hold all of them, and that is where they are edited anyway.
/// </summary>
public static class JiraKeyDisplay
{
    /// <summary>
    /// How many fit before a card starts growing. About two rows of chips in a grid cell, which is
    /// enough to recognise the work by without the card becoming a list.
    /// </summary>
    public const int Limit = 6;

    /// <summary>The keys to show on the card.</summary>
    public static IReadOnlyList<string> Visible(IReadOnlyList<string>? keys, int limit = Limit) =>
        keys is null ? [] : [.. keys.Take(Math.Max(0, limit))];

    /// <summary>
    /// What to put after them, or nothing when they all fit. One over the limit is shown rather than
    /// hidden behind "+1 more", which would take the same room and tell you less.
    /// </summary>
    public static string More(IReadOnlyList<string>? keys, int limit = Limit) =>
        keys is not null && keys.Count > limit ? $"+{keys.Count - limit} more" : string.Empty;

    /// <summary>Every key, for the tooltip on that overflow: hiding them must not lose them.</summary>
    public static string All(IReadOnlyList<string>? keys) =>
        keys is null or { Count: 0 } ? string.Empty : string.Join("  ", keys);
}
