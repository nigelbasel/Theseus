namespace Theseus.Core.Colour;

/// <summary>A colour returned to the pool when a workstream was archived.</summary>
/// <param name="ColourHex">The released colour.</param>
/// <param name="FreedUtc">When it was released. The oldest release is reused first.</param>
public sealed record TabColourRelease(string ColourHex, DateTimeOffset FreedUtc);
