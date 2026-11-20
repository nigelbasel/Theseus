namespace Theseus.Core.Projects;

/// <summary>
/// One entry of an evidence block's <c>gates</c> field. Information only: a session can write it, so it is a claim and
/// never passes a gate (spec AC 7, evidence-block Q3).
/// </summary>
/// <param name="Gate">The gate it names.</param>
/// <param name="Accepted">When it claims the gate was accepted, as written, or null.</param>
public sealed record EvidenceGateClaim(string Gate, string? Accepted);
