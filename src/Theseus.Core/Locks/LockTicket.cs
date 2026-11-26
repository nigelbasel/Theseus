namespace Theseus.Core.Locks;

/// <summary>
/// One request in a lock's queue, from the moment it is asked for until it is released. The ticket at
/// the front with <see cref="GrantedUtc"/> set is the holder; the rest wait in list order.
/// </summary>
/// <param name="Token">The secret the requester proves itself with; only it can attach or release.</param>
/// <param name="Lock">What it was asked as: a predefined scope's name, or the scope written out.</param>
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
/// <param name="TestHosts">Test hosts seen running under that process, to end if it dies without releasing.</param>
/// <param name="Scope">The resources it holds or waits for; empty on a ticket saved before scopes, until loaded.</param>
/// <param name="Owner">The Claude session behind it, when known, so a wait across two of its tickets is seen.</param>
/// <param name="Card">The card that asked, for telling a duplicate run.</param>
/// <param name="Command">The command line it runs, as sent.</param>
/// <param name="Commit">The repository's commit when it was asked for.</param>
/// <param name="Overtaken">How many later requests have been granted ahead of it on something it needs.</param>
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
    string? ProcessStart = null,
    IReadOnlyList<HeldProcess>? TestHosts = null,
    IReadOnlyList<ResourceClaim>? Scope = null,
    string? Owner = null,
    string? Card = null,
    string? Command = null,
    string? Commit = null,
    int Overtaken = 0,
    bool HeldAlone = false)
{
    private const string AloneKind = "held-alone:";

    /// <summary>Whether this ticket holds its lock.</summary>
    public bool IsHolder => GrantedUtc is not null;

    public IReadOnlyList<ResourceClaim> Claims => Scope ?? PredefinedScopes.Find(Lock, ResourceTable.Default) ?? [];

    /// <summary>
    /// What granting reads. A request that named a resource nobody had ruled on stays held alone, clashing with every
    /// hold, even after a ruling: a ruling applies only to requests made after it.
    /// </summary>
    public IReadOnlyList<ResourceClaim> GrantClaims => HeldAlone ? [.. Claims, new ResourceClaim(AloneKind + Token, 1)] : Claims;

    /// <summary>Whether its hold ends by itself, with its process, whatever else its owner waits for.</summary>
    public bool EndsByItself => Mode == HoldMode.Process;

    /// <summary>Who the ticket waits and holds as: its owner session when known, otherwise the ticket alone.</summary>
    public string OwnerKey => Owner ?? SessionId ?? $"ticket:{Token}";

    public bool Needs(string resource) =>
        Claims.Any(claim => string.Equals(claim.Resource, resource, StringComparison.Ordinal));
}
