using Theseus.Core.Stopping;

namespace Theseus.Core.Projects;

/// <summary>
/// Telling a card's session what the owner did on the board (1.1e AC 27, 27a; review of PR 6, finding 1): every event is
/// logged for events(); a running session is told with its next turn, joined to anything still waiting for it, through
/// the tells folder the stop hook reads; a start takes what waits, so nothing is told twice or left behind.
/// </summary>
public sealed class SessionTells
{
    private readonly StopRequestStore _waiting;
    private readonly OwnerEventLog _log;

    /// <summary>Creates the tells over the folder the hook reads and the owner event log.</summary>
    public SessionTells(StopRequestStore waiting, OwnerEventLog log)
    {
        _waiting = waiting;
        _log = log;
    }

    /// <summary>The tells folder's store, swept beside the stop requests.</summary>
    public StopRequestStore Waiting => _waiting;

    /// <summary>
    /// Logs it, and queues it for the session's next turn when the card is running with a session Theseus can address;
    /// whether it was queued. A card that is not running is never started from here.
    /// </summary>
    public bool Tell(string cardKey, string cardTitle, bool isRunning, string? sessionId, string text, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        _log.Add(new OwnerEvent(cardKey, text, now));

        return isRunning && Queue(cardKey, cardTitle, sessionId, text, now);
    }

    /// <summary>
    /// Queues it for the session's next turn, joined to anything still waiting, without logging it as the owner's: work a
    /// session hands to another card is that session's. Whether it was queued.
    /// </summary>
    public bool Queue(string cardKey, string cardTitle, string? sessionId, string text, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cardKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (string.IsNullOrWhiteSpace(sessionId) || StopRequestPath.For(_waiting.Root, sessionId) is null)
        {
            return false;
        }

        var before = _waiting.Waiting(sessionId)?.Text;
        _waiting.Write(new PendingStopRequest(sessionId, cardKey, cardTitle,
            before is null ? text : $"{before}{Environment.NewLine}{Environment.NewLine}{text}", now, now + AcceptedTell.Waits));

        return _waiting.Waiting(sessionId) is not null;
    }

    /// <summary>What waits for a session, taken for the start that resumes it, so its opening is told instead; null when none.</summary>
    public string? TakeForStart(string? sessionId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || _waiting.Waiting(sessionId) is not { } waiting)
        {
            return null;
        }

        _waiting.Claim(sessionId, now);
        _waiting.Retire(sessionId);

        return waiting.ExpiresUtc > now ? waiting.Text : null;
    }
}
