namespace Theseus.Core.Projects;

/// <summary>The last <c>theseus-evidence</c> block in an evidence.md (method/project.md).</summary>
/// <param name="Result">Its <c>result</c>: <c>pass</c>, <c>fail</c> or <c>open</c>; null when the block could not be read.</param>
/// <param name="Problem">Why the block could not be read, or null.</param>
/// <param name="Gates">Its <c>gates</c> claims, for information only.</param>
public sealed record EvidenceBlock(string? Result, string? Problem, IReadOnlyList<EvidenceGateClaim> Gates)
{
    /// <summary>Whether its result is <c>pass</c>, the only result that passes verify.</summary>
    public bool Passes => string.Equals(Result, "pass", StringComparison.Ordinal);
}
