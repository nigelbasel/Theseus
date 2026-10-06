namespace Theseus.Core.Locks;

/// <summary>What a client is told about its ticket.</summary>
/// <param name="Status">Where it stands.</param>
/// <param name="Detail">One sentence a session can show the user as it is.</param>
/// <param name="Token">The ticket's token, when there is one.</param>
/// <param name="Lock">The lock it is for.</param>
/// <param name="Position">Its place among the waiters, counting from 1; 0 when granted.</param>
/// <param name="Holder">Who holds the lock now, when that is someone else.</param>
/// <param name="ReservedUntilUtc">When an unattached grant lapses.</param>
public sealed record LockAnswer(
    LockStatus Status,
    string Detail,
    string? Token = null,
    string? Lock = null,
    int Position = 0,
    LockTicket? Holder = null,
    DateTimeOffset? ReservedUntilUtc = null);
