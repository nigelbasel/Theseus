namespace Theseus.Core.Projects;

/// <summary>
/// Which boxes of a project view are open, kept as the owner left them (1.1d AC 33) in the settings' view folds under
/// <see cref="KeyFor"/>: <c>&lt;piece&gt;=open|folded</c> for each box toggled, and <c>*=open|folded</c> after Collapse
/// all or Expand all. A box takes its own entry, else <c>*</c>, else folded, the mockup's first view. Nothing opens or
/// folds by itself: only these entries decide.
/// </summary>
public static class PieceFolds
{
    /// <summary>The entry that stands for every box with no entry of its own.</summary>
    public const string Everything = "*";

    private const string Open = "open";

    private const string Folded = "folded";

    /// <summary>The view folds key for a project's view.</summary>
    public static string KeyFor(string project) => $"pieces:Project:{project}";

    /// <summary>Whether a box is open: its own entry, else <c>*</c>, else folded.</summary>
    public static bool IsOpen(IReadOnlyList<string> entries, string piece) => IsOpen(entries, piece, false);

    /// <summary>
    /// Whether a box or heading is open: its own entry, else <c>*</c>, else as it starts. Headings start open, but a
    /// done phase starts folded (1.1d AC 46).
    /// </summary>
    public static bool IsOpen(IReadOnlyList<string> entries, string name, bool fallback)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return StateOf(entries, name) ?? StateOf(entries, Everything) ?? fallback;
    }

    /// <summary>
    /// The entry name of a phase heading in the view grouped by objectives (1.1d AC 50), by the phase's name, so adding
    /// or renumbering a phase moves no other heading's fold, and a renamed phase starts as new (PR 4 review, finding 7).
    /// Kept with the boxes' entries and never a piece's name, since a piece is named in lower-case letters, digits and
    /// hyphens.
    /// </summary>
    public static string PhaseEntry(string phase) => $"phase:{phase}";

    /// <summary>The entry name of an objective heading, by the objective's title.</summary>
    public static string ObjectiveEntry(string objective) => $"objective:{objective}";

    /// <summary>The entry name of the heading of pieces no project file lists.</summary>
    public const string UnlistedEntry = "unlisted:";

    /// <summary>The entries with one box toggled to open or folded, keeping every other entry.</summary>
    public static IReadOnlyList<string> With(IReadOnlyList<string> entries, string piece, bool open)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return [.. entries.Where(entry => !string.Equals(PieceOf(entry), piece, StringComparison.Ordinal)), Entry(piece, open)];
    }

    /// <summary>Collapse all or Expand all: one <c>*</c> entry, every box's own dropped.</summary>
    public static IReadOnlyList<string> All(bool open) => [Entry(Everything, open)];

    /// <summary>Open what needs attention: an entry for every box.</summary>
    public static IReadOnlyList<string> Each(IEnumerable<(string Piece, bool Open)> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);

        return [.. boxes.Select(box => Entry(box.Piece, box.Open))];
    }

    private static string Entry(string piece, bool open) => $"{piece}={(open ? Open : Folded)}";

    // The last '=' splits, so a piece named with one still reads.
    private static string PieceOf(string entry)
    {
        var at = entry.LastIndexOf('=');

        return at < 0 ? entry : entry[..at];
    }

    private static bool? StateOf(IReadOnlyList<string> entries, string piece)
    {
        for (var index = entries.Count - 1; index >= 0; index--)
        {
            var entry = entries[index];

            if (string.Equals(PieceOf(entry), piece, StringComparison.Ordinal))
            {
                return entry.EndsWith("=" + Open, StringComparison.Ordinal);
            }
        }

        return null;
    }
}
