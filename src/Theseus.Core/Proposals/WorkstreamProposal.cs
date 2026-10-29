namespace Theseus.Core.Proposals;

/// <summary>
/// A child workstream a session has asked for. Nothing exists until Nigel approves it on the board:
/// no card, no link, no tab. Proposals are never deleted, so a decided one still shows what was asked.
/// </summary>
/// <param name="Id">A short random identifier.</param>
/// <param name="Status">Where it stands.</param>
/// <param name="RequesterKey">The workstream that proposed it.</param>
/// <param name="SessionId">The session that sent it, kept as evidence: subagents share it.</param>
/// <param name="Request">What was asked for, as last sent.</param>
/// <param name="Warnings">What Nigel should look at before approving, worked out when it arrived.</param>
/// <param name="CreatedUtc">When it first arrived.</param>
/// <param name="DecidedUtc">When it left pending, or null.</param>
/// <param name="DecisionNote">Nigel's note on a rejection, or who withdrew it.</param>
/// <param name="ResultingKey">The card approval created, or null.</param>
public sealed record WorkstreamProposal(
    string Id,
    ProposalStatus Status,
    string RequesterKey,
    string SessionId,
    ProposalRequest Request,
    IReadOnlyList<string> Warnings,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? DecidedUtc = null,
    string? DecisionNote = null,
    string? ResultingKey = null)
{
    /// <summary>Whether it still waits for Nigel.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsPending => Status == ProposalStatus.Pending;
}
