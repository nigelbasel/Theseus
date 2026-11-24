namespace Theseus.Core.Breakers;

/// <summary>
/// A tripped breaker, kept on the card until the owner resets it. While it is there nothing starts the card
/// (objective 2.2).
/// </summary>
/// <param name="Kind">Which breaker.</param>
/// <param name="Detail">The sentence shown on the card and in the wait: what happened, with the figures.</param>
/// <param name="TrippedUtc">When it tripped.</param>
public sealed record BreakerTrip(BreakerKind Kind, string Detail, DateTimeOffset TrippedUtc);
