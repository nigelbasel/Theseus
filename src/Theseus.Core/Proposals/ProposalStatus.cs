namespace Theseus.Core.Proposals;

/// <summary>Where a proposed workstream stands. Only Nigel moves one out of pending, except its own proposer withdrawing it.</summary>
public enum ProposalStatus
{
    /// <summary>Waiting for Nigel on the board.</summary>
    Pending,

    /// <summary>Nigel created the card it asked for.</summary>
    Approved,

    /// <summary>Nigel turned it down.</summary>
    Rejected,

    /// <summary>The proposing workstream took it back before Nigel decided.</summary>
    Withdrawn,
}
