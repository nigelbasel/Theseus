namespace Theseus.Core.Stopping;

/// <summary>
/// What the stop hook hands a session at the end of a turn (1.1e AC 27; review of PR 6, finding 1): a stop request first,
/// else something it is told, from the tells folder; each claimed so it is given once.
/// </summary>
public static class HookTurn
{
    /// <summary>The request to deliver now, claimed; null when there is nothing for this turn.</summary>
    public static PendingStopRequest? Deliver(StopHookInput? input, StopRequestStore stops, StopRequestStore tells, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(stops);
        ArgumentNullException.ThrowIfNull(tells);

        if (input?.SessionId is not { } sessionId)
        {
            return null;
        }

        // Decided before claiming: claiming makes a request undeliverable to anyone else.
        foreach (var store in new[] { stops, tells })
        {
            if (StopHookDecision.Deliver(input, store.Waiting(sessionId), now) is not null && store.Claim(sessionId, now) is { } claimed)
            {
                return claimed;
            }
        }

        return null;
    }
}
