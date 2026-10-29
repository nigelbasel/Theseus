namespace Theseus.Core.Proposals;

/// <summary>What became of a proposal call: the proposal it made or touched, or why it was refused.</summary>
/// <param name="Proposal">The proposal, or null when refused.</param>
/// <param name="Refusal">Why nothing changed, or null.</param>
/// <param name="IsRepeat">Whether the call matched a pending proposal and updated it rather than making another.</param>
public sealed record ProposalOutcome(WorkstreamProposal? Proposal, string? Refusal, bool IsRepeat = false)
{
    /// <summary>A refusal.</summary>
    public static ProposalOutcome Refused(string refusal) => new(null, refusal);
}
