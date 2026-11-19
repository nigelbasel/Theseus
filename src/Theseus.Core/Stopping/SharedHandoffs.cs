using Theseus.Core.Domain;

namespace Theseus.Core.Stopping;

/// <summary>
/// Decides which cards stop sharing a handover file (handoff-names AC 1 to 4). Only cards that chose the shared file
/// themselves are moved, so a card that merely found it by search is never re-pointed. A move is skipped when it could
/// hand somebody else's file over, or start a second share: the new file must belong to no card, and no card that finds
/// its notes by search may have a name inside the new file's name.
/// </summary>
public static class SharedHandoffs
{
    /// <summary>The moves to make.</summary>
    /// <param name="cards">Every card on the board.</param>
    /// <param name="exists">Whether a file exists.</param>
    /// <param name="inRepository">Whether a folder is inside a git working tree, where a new file could be committed.</param>
    /// <param name="today">The date the pointer line gives.</param>
    public static IReadOnlyList<HandoffMove> Plan(
        IReadOnlyList<HandoffCard> cards, Func<string, bool> exists, Func<string, bool> inRepository, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(exists);
        ArgumentNullException.ThrowIfNull(inRepository);

        var moves = new List<HandoffMove>();

        foreach (var group in cards.Where(card => card.IsChosen).GroupBy(card => card.File, StringComparer.OrdinalIgnoreCase))
        {
            var sharers = group.ToList();

            if (sharers.Count < 2 || inRepository(Path.GetDirectoryName(group.Key) ?? string.Empty))
            {
                continue;
            }

            var ownerKey = HandoffOwner.Of(group.Key, [.. sharers.Select(card => (card.Key, card.Title))]);
            var owner = sharers.FirstOrDefault(card => string.Equals(card.Key, ownerKey, StringComparison.OrdinalIgnoreCase));

            if (owner is null)
            {
                continue;
            }

            foreach (var card in sharers.Where(card => card != owner && !card.IsBusy))
            {
                var own = HandoffPointer.OwnFile(group.Key, card.Key);

                if (IsTaken(own, card, cards))
                {
                    continue;
                }

                moves.Add(new HandoffMove(card.Key, card.Title, owner.Title, group.Key, own,
                    exists(own) ? null : HandoffPointer.Text(group.Key, today)));
            }
        }

        return moves;
    }

    private static bool IsTaken(string own, HandoffCard moving, IReadOnlyList<HandoffCard> cards)
    {
        var name = Path.GetFileNameWithoutExtension(own);

        return cards.Where(card => card != moving).Any(card =>
            string.Equals(card.File, own, StringComparison.OrdinalIgnoreCase)
            || (!card.IsChosen && (Mentions(name, card.Key) || Mentions(name, WorkstreamKey.FromTitle(card.Title)))));
    }

    private static bool Mentions(string name, string token) =>
        token.Length > 0 && name.Contains(token, StringComparison.OrdinalIgnoreCase);
}
