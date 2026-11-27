using Theseus.Core.Domain;

namespace Theseus.Core.Links;

/// <summary>
/// The card a card's runs count as when telling a repeat run: a verifier's are the card it verifies, so a verifier
/// and the fix it checks running the same command on the same commit are one run.
/// </summary>
public static class RunFamily
{
    public static string Of(string card, BoardSnapshot board, IReadOnlyList<Link> links)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(links);

        if (board.Card(card)?.Workstream.Role != WorkstreamRole.Verifier)
        {
            return card;
        }

        return links
            .Where(link => link.IsActive && link.Kind == LinkKind.ReportsBack && string.Equals(link.From, card, StringComparison.OrdinalIgnoreCase))
            .OrderBy(link => link.CreatedUtc)
            .LastOrDefault()?.To ?? card;
    }
}
