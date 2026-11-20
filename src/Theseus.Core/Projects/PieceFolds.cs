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
    public static bool IsOpen(IReadOnlyList<string> entries, string piece)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return StateOf(entries, piece) ?? StateOf(entries, Everything) ?? false;
    }

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
