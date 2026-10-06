namespace Theseus.Core.Locks;

/// <summary>One line of the lock history, enough to say later what waiting and holding cost.</summary>
/// <param name="WhenUtc">When it happened.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Lock">Which lock.</param>
/// <param name="Who">Whose ticket.</param>
/// <param name="Purpose">What the ticket was for.</param>
/// <param name="WaitedSeconds">How long it had waited, when it was granted or left the queue.</param>
/// <param name="HeldSeconds">How long it had held the lock, when it let it go or lost it.</param>
/// <param name="Detail">Anything else worth knowing.</param>
public sealed record LockEvent(
    DateTimeOffset WhenUtc,
    LockEventKind Kind,
    string Lock,
    string Who,
    string Purpose,
    double? WaitedSeconds = null,
    double? HeldSeconds = null,
    string? Detail = null);
