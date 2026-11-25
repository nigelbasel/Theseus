namespace Theseus.Core.Projects;

/// <summary>
/// Something the owner did on the board that concerns a card (1.1e AC 27a): a proposal of its approved or rejected, a
/// report to it confirmed, its spec accepted. Its session is told, and events() lists it.
/// </summary>
/// <param name="CardKey">The card it concerns.</param>
/// <param name="Text">What it is told.</param>
/// <param name="AtUtc">When the owner did it.</param>
public sealed record OwnerEvent(string CardKey, string Text, DateTimeOffset AtUtc);
