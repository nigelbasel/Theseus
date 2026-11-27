namespace Theseus.Core.Attention;

/// <summary>One session working, and the card it works for (7.3: every busy session of a card counts, not only its last).</summary>
/// <param name="Workstream">The card's key.</param>
/// <param name="SessionId">The session, or null when not known.</param>
/// <param name="Stamp">What the card was part of at the moment of the sample, or null.</param>
public sealed record BusySession(string Workstream, string? SessionId, AttentionStamp? Stamp = null);
