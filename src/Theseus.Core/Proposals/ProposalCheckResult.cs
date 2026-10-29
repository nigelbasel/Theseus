namespace Theseus.Core.Proposals;

/// <summary>Whether a proposal may be filed, and what Nigel should be warned about if it is.</summary>
/// <param name="Refusal">Why it may not be filed, or null.</param>
/// <param name="Warnings">Things worth a look at approval, which never refuse it.</param>
public sealed record ProposalCheckResult(string? Refusal, IReadOnlyList<string> Warnings)
{
    /// <summary>A refusal.</summary>
    public static ProposalCheckResult Refused(string refusal) => new(refusal, []);
}
