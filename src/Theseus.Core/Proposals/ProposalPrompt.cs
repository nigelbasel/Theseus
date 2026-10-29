namespace Theseus.Core.Proposals;

/// <summary>
/// The opening prompt of a card made from a proposal. It points at the brief rather than copying it,
/// because the brief is the authority and may be revised before the card first runs, and it says in so
/// many words what approval did not authorise. The links themselves reach the session through its
/// related-workstreams paragraph and the <c>related</c> tool.
/// </summary>
public static class ProposalPrompt
{
    /// <summary>The prompt for a card approved from this request.</summary>
    public static string For(ProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return string.Join("\n\n",
            $"Read your brief first: {request.Brief.Trim()}",
            $"Done when: {request.DoneCriteria.Trim()}",
            request.ReportBack
                ? "When that is done, write your report where your report-back link asks (call related to see it), then call report_back."
                : "You carry this work on from the card that proposed you; call related to see it and the document it handed over.",
            "Your brief authorises no Jira item, branch, worktree, push or pull request. Ask the user before any of those.");
    }
}
