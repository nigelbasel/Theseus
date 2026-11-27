namespace Theseus.Core.Attention;

/// <summary>What a card was part of at one moment, stamped on the attention lines written for it (7.3).</summary>
/// <param name="Project">Its project, or null for ad hoc work.</param>
/// <param name="PieceId">The stable id of its piece of work, or null when it is in none.</param>
/// <param name="Stage">The stage its session last reported, or null.</param>
public sealed record AttentionStamp(string? Project, string? PieceId, string? Stage);
