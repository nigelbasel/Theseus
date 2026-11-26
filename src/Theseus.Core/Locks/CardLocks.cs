namespace Theseus.Core.Locks;

/// <summary>
/// A card's holds and places in the lock queues, for Stop the project (project-view-as-tabs AC 10, Nigel 2026-11-26: "tell
/// all its workstreams to stop and release their locks"). Only the owner's Stop the project calls it, and only once the
/// card's session has ended. A process hold is never released here: it ends when its run exits, and ending it early would
/// let the next run start beside one still running.
/// </summary>
public static class CardLocks
{
    /// <summary>The tickets that belong to a card: asked by it, or by its session.</summary>
    /// <param name="tickets">Every ticket, as the lock book has them.</param>
    /// <param name="card">The card's key.</param>
    /// <param name="sessionId">The card's session, or null when it has none.</param>
    public static IReadOnlyList<LockTicket> Of(IEnumerable<LockTicket> tickets, string card, string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentNullException.ThrowIfNull(card);

        return
        [
            .. tickets.Where(ticket => string.Equals(ticket.Card, card, StringComparison.OrdinalIgnoreCase)
                                       || (sessionId is not null
                                           && (string.Equals(ticket.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)
                                               || string.Equals(ticket.Owner, sessionId, StringComparison.OrdinalIgnoreCase)))),
        ];
    }

    /// <summary>Whether Stop the project may release a ticket: a session hold or a place in a queue, never a process hold.</summary>
    public static bool Releasable(LockTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        return !ticket.IsHolder || ticket.Mode == HoldMode.Session;
    }

    /// <summary>Releases a stopped card's session holds and takes it out of every queue; how many tickets ended.</summary>
    /// <param name="book">The lock book.</param>
    /// <param name="card">The card's key.</param>
    /// <param name="sessionId">The card's session, or null when it has none.</param>
    public static int Release(LockBook book, string card, string? sessionId)
    {
        ArgumentNullException.ThrowIfNull(book);

        var released = 0;

        foreach (var ticket in Of(book.Snapshot(), card, sessionId).Where(Releasable))
        {
            if (ticket.IsHolder ? book.ForceReleaseTicket(ticket.Token) : book.Cancel(ticket.Token))
            {
                released++;
            }
        }

        return released;
    }
}
