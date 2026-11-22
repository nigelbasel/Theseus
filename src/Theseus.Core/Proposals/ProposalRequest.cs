namespace Theseus.Core.Proposals;

/// <summary>What a session asks for when it proposes a child workstream.</summary>
/// <param name="Title">The new workstream's title.</param>
/// <param name="Brief">The absolute path of the brief the child starts from. It must exist.</param>
/// <param name="ReportBack">Whether the child reports back to the proposer when it is done.</param>
/// <param name="ExpectedOutput">Where the child writes its report, or null for the brief folder's default.</param>
/// <param name="DoneCriteria">What the child has to produce to be finished.</param>
/// <param name="JiraKeys">Issues the child carries; each must already be on the proposer's card.</param>
/// <param name="Reason">Why the child is wanted, shown on the board.</param>
public sealed record ProposalRequest(
    string Title,
    string Brief,
    bool ReportBack,
    string? ExpectedOutput,
    string DoneCriteria,
    IReadOnlyList<string> JiraKeys,
    string? Reason)
{
    /// <summary>
    /// The slug of the new card's records folder, when the proposer is in a project: a new one, not in the project's
    /// table yet. On approval its row is added to the project file under the proposer's objective (1.1c AC 3).
    /// </summary>
    public string? Slug { get; init; }

    /// <summary>
    /// The objective the new workstream serves, by number or title (1.1e AC 31): its row is written under it on approval.
    /// Required of a proposal from a card in a project.
    /// </summary>
    public string? Objective { get; init; }

    /// <summary>The new workstream's branch, for its row; none named, the row says <c>none</c>.</summary>
    public string? Branch { get; init; }

    /// <summary>The new workstream's repos, for its row; none named, the row says <c>read-only</c>.</summary>
    public IReadOnlyList<string> Repos { get; init; } = [];
}
