using Theseus.Core.Links;

namespace Theseus.Core.Projects;

/// <summary>
/// Which piece handed work to which (1.1f D15, D16): a hand-over's parent is the From card's piece its
/// <see cref="Link.ParentPiece"/> names; its child is the To card's current piece. A link older than ParentPiece names
/// none, and takes the From card's last piece in the child's round: its current piece when the project has one round,
/// as 1.1d placed it (decision G8, refining D6, which assumed every old link was made in round 1). A link whose parent
/// piece is not in this project places nothing here.
/// </summary>
internal static class PieceHandOvers
{
    /// <summary>Each standing hand-over as parent and child indexes into <paramref name="pieces"/>; a child's first wins.</summary>
    /// <param name="pieces">The pieces, in file order.</param>
    /// <param name="cardOf">A piece's card key.</param>
    /// <param name="nameOf">A piece's name.</param>
    /// <param name="isCurrent">Whether a piece is its card's current piece.</param>
    /// <param name="roundOf">A piece's round, or null when it has none of its own (a piece no project file lists).</param>
    /// <param name="links">The links.</param>
    public static List<(int Parent, int Child)> Resolve<T>(IReadOnlyList<T> pieces, Func<T, string> cardOf, Func<T, string> nameOf,
        Func<T, bool> isCurrent, Func<T, int?> roundOf, IEnumerable<Link> links)
    {
        var resolved = new List<(int Parent, int Child)>();
        var placed = new HashSet<int>();

        foreach (var link in PieceTree.HandOvers(links))
        {
            var child = IndexOf(pieces, index => Same(cardOf(pieces[index]), link.To) && isCurrent(pieces[index]));

            if (child < 0)
            {
                continue;
            }

            var parent = ParentOf(pieces, cardOf, nameOf, isCurrent, roundOf, roundOf(pieces[child]), link);

            if (parent >= 0 && parent != child && placed.Add(child))
            {
                resolved.Add((parent, child));
            }
        }

        return resolved;
    }

    private static int ParentOf<T>(IReadOnlyList<T> pieces, Func<T, string> cardOf, Func<T, string> nameOf, Func<T, bool> isCurrent,
        Func<T, int?> roundOf, int? childRound, Link link)
    {
        bool From(int index) => Same(cardOf(pieces[index]), link.From);

        if (link.ParentPiece is { } named)
        {
            return IndexOf(pieces, index => From(index) && Same(nameOf(pieces[index]), named));
        }

        var inRound = childRound is null ? -1 : LastIndexOf(pieces, index => From(index) && roundOf(pieces[index]) == childRound);

        if (inRound >= 0)
        {
            return inRound;
        }

        var current = LastIndexOf(pieces, index => From(index) && isCurrent(pieces[index]));

        return current >= 0 ? current : IndexOf(pieces, From);
    }

    private static int IndexOf<T>(IReadOnlyList<T> pieces, Func<int, bool> matches)
    {
        for (var index = 0; index < pieces.Count; index++)
        {
            if (matches(index))
            {
                return index;
            }
        }

        return -1;
    }

    private static int LastIndexOf<T>(IReadOnlyList<T> pieces, Func<int, bool> matches)
    {
        for (var index = pieces.Count - 1; index >= 0; index--)
        {
            if (matches(index))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
