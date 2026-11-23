namespace Theseus.Core.Proposals;

/// <summary>What became of approving a proposal from its session: the card it made, or why not.</summary>
/// <param name="Key">The new card's key, when it was made.</param>
/// <param name="Refusal">Why nothing was made, or null.</param>
public sealed record ProposalApproval(string? Key, string? Refusal)
{
    /// <summary>What went short when the card was made, such as its project row not being written; null when nothing did.</summary>
    public string? Shortfall { get; init; }

    /// <summary>An approval that made its card.</summary>
    public static ProposalApproval Made(string key) => new(key, null);

    /// <summary>An approval that made nothing.</summary>
    public static ProposalApproval Refused(string refusal) => new(null, refusal);
}
