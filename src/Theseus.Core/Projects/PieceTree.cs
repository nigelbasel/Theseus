using Theseus.Core.Links;

namespace Theseus.Core.Projects;

/// <summary>
/// A project's pieces of work as a tree: time runs down the page, and work handed on sits under the piece that handed
/// it on (1.1d AC 11 to 14; Nigel, 2026-11-20: "scrolling right is less natural than scrolling down"). Links join
/// cards, so each end is the card's current piece (code fact 3).
/// </summary>
public sealed class PieceTree
{
    private PieceTree(IReadOnlyList<PieceNode> roots, IReadOnlyList<ThenWorkedOnLink> thenWorkedOn)
    {
        Roots = roots;
        ThenWorkedOn = thenWorkedOn;
    }

    /// <summary>The pieces with no parent in the project, in file order.</summary>
    public IReadOnlyList<PieceNode> Roots { get; }

    /// <summary>Each piece joined to the next row of the same workstream, in file order.</summary>
    public IReadOnlyList<ThenWorkedOnLink> ThenWorkedOn { get; }

    /// <summary>Every node, depth first from the roots.</summary>
    public IEnumerable<PieceNode> All => Roots.SelectMany(Flatten);

    /// <summary>
    /// Places each piece under the piece whose card hands work to its card. A second parent is ignored, and a cycle is
    /// broken at its piece met first in file order, as <see cref="ChainRoots"/> stops at the card it has met.
    /// </summary>
    /// <param name="pieces">The project's pieces, as <see cref="PiecesOfWork.Build"/> gave them.</param>
    /// <param name="links">The links read against the board.</param>
    public static PieceTree Build(IReadOnlyList<PieceOfWork> pieces, LinkStatus links)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(links);

        var currentOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < pieces.Count; index++)
        {
            if (pieces[index].IsCurrentPiece)
            {
                currentOf[pieces[index].CardKey] = index;
            }
        }

        var parentOf = new Dictionary<int, int>();

        foreach (var link in HandOvers(links.Links))
        {
            if (currentOf.TryGetValue(link.From, out var parent) && currentOf.TryGetValue(link.To, out var child) && parent != child)
            {
                parentOf.TryAdd(child, parent);
            }
        }

        BreakCycles(pieces.Count, parentOf);

        var childrenOf = parentOf.GroupBy(pair => pair.Value)
            .ToDictionary(group => group.Key, group => group.Select(pair => pair.Key).Order().ToList());
        var marks = Marks(pieces, currentOf, links);

        PieceNode Node(int index) => new(pieces[index],
            [.. childrenOf.GetValueOrDefault(index, []).Select(Node)],
            pieces[index].IsCurrentPiece ? marks.GetValueOrDefault(pieces[index].CardKey) : null);

        var roots = Enumerable.Range(0, pieces.Count).Where(index => !parentOf.ContainsKey(index)).Select(Node).ToList();

        return new PieceTree(roots, ThenWorkedOnLinks(pieces));
    }

    /// <summary>
    /// The links that place one piece under another: hand-overs still standing. <see cref="PiecesOfWork"/> makes
    /// unlisted pieces from the same set.
    /// </summary>
    internal static IEnumerable<Link> HandOvers(IEnumerable<Link> links) =>
        links.Where(link => link.IsActive && link.Kind == LinkKind.HandsOver);

    private static void BreakCycles(int count, Dictionary<int, int> parentOf)
    {
        for (var index = 0; index < count; index++)
        {
            var seen = new HashSet<int> { index };
            var at = index;

            while (parentOf.TryGetValue(at, out var parent))
            {
                if (parent == index)
                {
                    parentOf.Remove(index);

                    break;
                }

                if (!seen.Add(parent))
                {
                    break;
                }

                at = parent;
            }
        }
    }

    private static Dictionary<string, ReportBackMark> Marks(IReadOnlyList<PieceOfWork> pieces, Dictionary<string, int> currentOf,
        LinkStatus links)
    {
        var marks = new Dictionary<string, ReportBackMark>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links.Links.Where(link => link.Kind == LinkKind.ReportsBack))
        {
            var state = links.StateOf(link);

            if (!currentOf.ContainsKey(link.From) || !currentOf.TryGetValue(link.To, out var parent) || state == LinkState.CardMissing)
            {
                continue;
            }

            marks.TryAdd(link.From, new ReportBackMark(pieces[parent].Name, state));
        }

        return marks;
    }

    private static List<ThenWorkedOnLink> ThenWorkedOnLinks(IReadOnlyList<PieceOfWork> pieces)
    {
        var last = new Dictionary<string, PieceOfWork>(StringComparer.OrdinalIgnoreCase);
        var links = new List<ThenWorkedOnLink>();

        foreach (var piece in pieces.Where(piece => piece.IsListed))
        {
            if (last.TryGetValue(piece.CardKey, out var earlier))
            {
                links.Add(new ThenWorkedOnLink(earlier, piece));
            }

            last[piece.CardKey] = piece;
        }

        return links;
    }

    private static IEnumerable<PieceNode> Flatten(PieceNode node) => node.Children.SelectMany(Flatten).Prepend(node);
}
