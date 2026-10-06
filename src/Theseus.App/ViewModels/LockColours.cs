using Theseus.Core.Locks;

namespace Theseus.App.ViewModels;

/// <summary>
/// Which card a lock ticket belongs to, so the locks panel can mark each entry with that workstream's
/// tab colour (Nigel, 2026-10-06). A session hold names its session, which is exact. A test run names
/// itself as the session's name from ListAgents, which is the card's title, so that is matched next,
/// then the workstream key. Anything else gets no colour rather than a guessed one.
/// </summary>
public static class LockColours
{
    /// <summary>The tab colour of the card the ticket belongs to, or null when none matches.</summary>
    public static string? For(LockTicket ticket, IEnumerable<WorkstreamCardViewModel> cards)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var all = cards.ToList();
        var card = (ticket.SessionId is { } sessionId
                       ? all.FirstOrDefault(candidate => string.Equals(candidate.Workstream.LastSessionId, sessionId, StringComparison.OrdinalIgnoreCase))
                       : null)
                   ?? all.FirstOrDefault(candidate => string.Equals(candidate.Title, ticket.Who, StringComparison.OrdinalIgnoreCase))
                   ?? all.FirstOrDefault(candidate => string.Equals(candidate.Key, ticket.Who, StringComparison.OrdinalIgnoreCase));

        return card?.ColorHex;
    }
}
