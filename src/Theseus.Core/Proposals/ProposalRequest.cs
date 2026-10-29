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
    string? Reason);
