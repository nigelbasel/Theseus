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
    string? Detail = null,
    string? Scope = null,
    string? Command = null,
    string? Commit = null,
    int? ExitCode = null)
{
    /// <summary>
    /// The card the ticket was taken for, when it said, so the wait can be taken out of that card's machine time (7.3).
    /// Lines written before 7.3 have none and are matched to a card by <see cref="Who"/>.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Card { get; init; }

    /// <summary>The session the ticket was taken for, when it said (7.3).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Session { get; init; }
}
