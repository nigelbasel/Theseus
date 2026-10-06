namespace Theseus.Core.Locks;

/// <summary>
/// One request in a lock's queue, from the moment it is asked for until it is released. The ticket at
/// the front with <see cref="GrantedUtc"/> set is the holder; the rest wait in list order.
/// </summary>
/// <param name="Token">The secret the requester proves itself with; only it can attach or release.</param>
/// <param name="Lock">Which lock.</param>
/// <param name="Who">Who asked.</param>
/// <param name="Purpose">What for.</param>
/// <param name="Mode">What keeps it held once granted.</param>
/// <param name="RequesterKey">Identifies the same request asked again, so a requeue keeps its place.</param>
/// <param name="RequestedUtc">When it joined the queue.</param>
/// <param name="LastSeenUtc">When the requester last asked about it; a waiter silent too long is dropped.</param>
/// <param name="WorkingDirectory">Where the work runs.</param>
/// <param name="SessionId">The Claude session, for a session hold.</param>
/// <param name="Reason">Why it matters, as the requester put it.</param>
/// <param name="GrantedUtc">When it was granted, or null while waiting.</param>
/// <param name="ReservedUntilUtc">For a process hold not yet attached, when the grant lapses.</param>
/// <param name="ProcessId">The Windows process holding it, once attached.</param>
/// <param name="ProcessStart">That process's start time as a file time, so a reused id is not mistaken for it.</param>
public sealed record LockTicket(
    string Token,
    string Lock,
    string Who,
    string Purpose,
    HoldMode Mode,
    string RequesterKey,
    DateTimeOffset RequestedUtc,
    DateTimeOffset LastSeenUtc,
    string? WorkingDirectory = null,
    string? SessionId = null,
    string? Reason = null,
    DateTimeOffset? GrantedUtc = null,
    DateTimeOffset? ReservedUntilUtc = null,
    int? ProcessId = null,
    string? ProcessStart = null)
{
    /// <summary>Whether this ticket holds its lock.</summary>
    public bool IsHolder => GrantedUtc is not null;
}
