using Theseus.Core.Domain;
using Theseus.Core.Locks;

namespace Theseus.Core.Attention;

/// <summary>
/// The lock waits in the lock history (7.3). Theseus grants the tests, dev-stack and llama locks, so it knows when each
/// session joined a queue and when it was granted; a session waiting reads as busy, and 46-minute stalls are on record,
/// so that time is taken out of machine time and shown on its own.
/// </summary>
public static class LockWaits
{
    /// <summary>Every wait the history records: each grant, and each ticket that left the queue without one.</summary>
    /// <param name="events">The lock history.</param>
    /// <param name="workstreams">The board's cards, to tell the card of a line written before 7.3 from its holder's name.</param>
    public static IReadOnlyList<LockWait> From(IReadOnlyList<LockEvent> events, IReadOnlyList<Workstream> workstreams)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(workstreams);

        var byName = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. events
                .Where(lockEvent => lockEvent.Kind is LockEventKind.Granted or LockEventKind.WaiterDropped or LockEventKind.Cancelled
                                    && lockEvent.WaitedSeconds is > 0)
                .Select(lockEvent => new LockWait(
                    lockEvent.Card ?? CardNamed(lockEvent.Who, workstreams, byName),
                    lockEvent.Session,
                    lockEvent.Lock,
                    lockEvent.WhenUtc - TimeSpan.FromSeconds(lockEvent.WaitedSeconds!.Value),
                    lockEvent.WhenUtc)),
        ];
    }

    // A ticket's holder is named by its session's name, which is the card's tab title.
    private static string? CardNamed(string who, IReadOnlyList<Workstream> workstreams, Dictionary<string, string?> byName)
    {
        if (!byName.TryGetValue(who, out var card))
        {
            card = workstreams.FirstOrDefault(workstream => LooseName.Same(workstream.Title, who) || LooseName.Same(workstream.Key, who))?.Key;
            byName[who] = card;
        }

        return card;
    }
}
