using Theseus.Core.Links;

namespace Theseus.Core.Projects;

/// <summary>
/// Which piece handed work to which (1.1f D15, D16, D6): a hand-over's parent is the From card's piece its
/// <see cref="Link.ParentPiece"/> names, or the From card's first piece in file order when the link is older than that;
/// its child is the To card's current piece. A link whose parent piece is not in this project places nothing here.
/// </summary>
internal static class PieceHandOvers
{
    /// <summary>Each standing hand-over as parent and child indexes into <paramref name="pieces"/>; a child's first wins.</summary>
    public static List<(int Parent, int Child)> Resolve<T>(IReadOnlyList<T> pieces, Func<T, string> cardOf, Func<T, string> nameOf,
        Func<T, bool> isCurrent, IEnumerable<Link> links)
    {
        var resolved = new List<(int Parent, int Child)>();
        var placed = new HashSet<int>();

        foreach (var link in PieceTree.HandOvers(links))
        {
            var parent = ParentOf(pieces, cardOf, nameOf, link);
            var child = IndexOf(pieces, index => Same(cardOf(pieces[index]), link.To) && isCurrent(pieces[index]));

            if (parent >= 0 && child >= 0 && parent != child && placed.Add(child))
            {
                resolved.Add((parent, child));
            }
        }

        return resolved;
    }

    private static int ParentOf<T>(IReadOnlyList<T> pieces, Func<T, string> cardOf, Func<T, string> nameOf, Link link) =>
        link.ParentPiece is { } named
            ? IndexOf(pieces, index => Same(cardOf(pieces[index]), link.From) && Same(nameOf(pieces[index]), named))
            : IndexOf(pieces, index => Same(cardOf(pieces[index]), link.From));

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

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
