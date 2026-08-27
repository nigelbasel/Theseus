namespace Theseus.Core.Stopping;

/// <summary>
/// A stop request waiting to be delivered to one session.
/// </summary>
/// <param name="SessionId">The session it is for.</param>
/// <param name="WorkstreamKey">The workstream that asked, for the board's own reporting.</param>
/// <param name="WorkstreamTitle">What to call that workstream when telling the user what happened.</param>
/// <param name="Text">What the session is asked to do, with its handoff path already filled in.</param>
/// <param name="RequestedUtc">When the request was made.</param>
/// <param name="ExpiresUtc">
/// When it stops being worth delivering. An undelivered request would otherwise wait indefinitely
/// and arrive on the session's next turn - which for a session resumed the following morning means
/// interrupting different work with an instruction about yesterday's.
/// </param>
public sealed record PendingStopRequest(
    string SessionId,
    string WorkstreamKey,
    string WorkstreamTitle,
    string Text,
    DateTimeOffset RequestedUtc,
    DateTimeOffset ExpiresUtc);
