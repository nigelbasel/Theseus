namespace Theseus.Core.Proposals;

/// <summary>
/// The cards a session may start, stop, pause and resume (1.1e AC 14; Nigel, 2026-11-21: "we should also allow projects to
/// stop and start and pause and restart existing workstreams that they have originally created"): those made from a
/// proposal its card raised and the owner approved, and their descendants in turn. Worked out from Theseus's own proposal
/// records, never from what the caller says. A card a project's session made itself is not counted: the owner never
/// approved it, so an ad hoc card that was once its ancestor gains no hold on it (review of sessions-run-the-project PR 1,
/// finding 1); inside a project, membership decides instead.
/// </summary>
public static class WorkstreamDescendants
{
    /// <summary>The keys of a card's descendants, nearest first; never the card itself.</summary>
    /// <param name="callerKey">The calling session's card.</param>
    /// <param name="proposals">Every proposal Theseus has recorded.</param>
    public static IReadOnlyList<string> Of(string callerKey, IReadOnlyList<WorkstreamProposal> proposals)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callerKey);
        ArgumentNullException.ThrowIfNull(proposals);

        var approved = proposals
            .Where(proposal => proposal is { Status: ProposalStatus.Approved, MadeBySession: false, ResultingKey: { Length: > 0 } })
            .ToList();
        var found = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { callerKey };
        var frontier = new Queue<string>([callerKey]);

        while (frontier.Count > 0)
        {
            var parent = frontier.Dequeue();

            foreach (var child in approved.Where(proposal => string.Equals(proposal.RequesterKey, parent, StringComparison.OrdinalIgnoreCase))
                         .Select(proposal => proposal.ResultingKey!))
            {
                if (seen.Add(child))
                {
                    found.Add(child);
                    frontier.Enqueue(child);
                }
            }
        }

        return found;
    }
}
