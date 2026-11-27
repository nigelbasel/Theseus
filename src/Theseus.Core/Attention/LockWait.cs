namespace Theseus.Core.Attention;

/// <summary>A stretch a card's session spent queued for one of the machine's locks (7.3).</summary>
/// <param name="Workstream">The card it waited for, or null when it could not be told.</param>
/// <param name="Session">The session that waited, or null when the ticket did not say.</param>
/// <param name="Lock">The lock.</param>
/// <param name="From">When it joined the queue.</param>
/// <param name="To">When it was granted the lock, or left the queue without it.</param>
public sealed record LockWait(string? Workstream, string? Session, string Lock, DateTimeOffset From, DateTimeOffset To);
