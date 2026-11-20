using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// What a project card counts as waiting on the owner (project-view AC 12): its sessions waiting for input, reports
/// waiting for his confirmation, proposals pending from its cards, and specs waiting at their gate. One line each.
/// </summary>
public static class ProjectWaiting
{
    /// <summary>The lines, in that order.</summary>
    /// <param name="members">The project's cards.</param>
    /// <param name="reports">Reports waiting for confirmation, by link: the reporting card and the card reported to.</param>
    /// <param name="proposals">Pending proposals: the proposing card's key and the proposal's title.</param>
    /// <param name="specs">Specs waiting at their gate.</param>
    public static IReadOnlyList<string> For(IReadOnlyList<WorkstreamCardViewModel> members, IEnumerable<Link> reports,
        IEnumerable<(string RequesterKey, string Title)> proposals, IReadOnlyList<SpecAtGate> specs)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(reports);
        ArgumentNullException.ThrowIfNull(proposals);
        ArgumentNullException.ThrowIfNull(specs);

        var keys = members.ToDictionary(card => card.Key, StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();

        lines.AddRange(members
            .Where(card => card.Lifecycle == SessionLifecycle.NeedsAttention)
            .Select(card => $"{card.Title} is waiting for your input"));

        lines.AddRange(reports
            .Where(link => keys.ContainsKey(link.From) || keys.ContainsKey(link.To))
            .Select(link => $"A report from {Title(keys, link.From)} waits for your confirmation"));

        lines.AddRange(proposals
            .Where(proposal => keys.ContainsKey(proposal.RequesterKey))
            .Select(proposal => $"{Title(keys, proposal.RequesterKey)} proposes {proposal.Title}"));

        lines.AddRange(specs.Select(spec => $"The spec of {spec.Slug} waits for you to accept it"));

        return lines;
    }

    private static string Title(IReadOnlyDictionary<string, WorkstreamCardViewModel> keys, string key) =>
        keys.TryGetValue(key, out var card) ? card.Title : key;
}
