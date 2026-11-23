using Theseus.Core.Finding;

namespace Theseus.App.ViewModels;

/// <summary>
/// Which boxes of a project view the board's search carries into it (1.3 item 7): a box whose card matches, whose title
/// matches when it has no card, or which a matching verifier checks. A verifier has no box of its own, so a search that
/// found only verifiers lit the project and then highlighted nothing in it (project-view-usability item 4, review).
/// </summary>
public static class SearchCarry
{
    /// <summary>Whether the box is highlighted.</summary>
    /// <param name="node">The box.</param>
    /// <param name="cardMatches">Whether a board card matches, by key; null when the board has no such card.</param>
    /// <param name="filter">The board's search.</param>
    public static bool Matches(PieceNodeViewModel node, Func<string, bool?> cardMatches, CardFilter filter)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(cardMatches);
        ArgumentNullException.ThrowIfNull(filter);

        if (node.Piece.PendingVerifiers.Any(verifier => cardMatches(verifier.Key) == true))
        {
            return true;
        }

        return node.Piece.Card is { } piece && cardMatches(piece.Key) is { } matched
            ? matched
            : filter.Admits(new SearchableCard(node.Name, node.Title, string.Empty, string.Empty, [], [], [], []));
    }
}
