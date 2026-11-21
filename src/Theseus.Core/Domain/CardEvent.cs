namespace Theseus.Core.Domain;

/// <summary>
/// Something done to a card that is not a run of its own, such as a project's session stopping or pausing it (1.1e AC 16):
/// kept so the card's Properties can show its history.
/// </summary>
/// <param name="AtUtc">When.</param>
/// <param name="CardKey">The card it was done to.</param>
/// <param name="Text">What was done, such as "Paused by Theseus: waiting for the spec".</param>
public sealed record CardEvent(DateTimeOffset AtUtc, string CardKey, string Text);
