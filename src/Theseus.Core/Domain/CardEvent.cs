namespace Theseus.Core.Domain;

/// <summary>
/// Something done to a card that is not a run of its own, such as a project's session stopping or pausing it (1.1e AC 16):
/// kept so the card's Properties can show its history.
/// </summary>
/// <param name="AtUtc">When.</param>
/// <param name="CardKey">The card it was done to.</param>
/// <param name="Text">What was done, such as "Paused by Theseus: waiting for the spec".</param>
public sealed record CardEvent(DateTimeOffset AtUtc, string CardKey, string Text)
{
    /// <summary>The card whose session did it, for a start or resume given work; null otherwise.</summary>
    public string? By { get; init; }

    /// <summary>All of the instruction it was started or resumed with, a hand-in; null when it was given none.</summary>
    public string? Instruction { get; init; }
}
