using Theseus.Core.Links;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// Why each card is stopped (1.1e AC 12), from what the board knows: its last stage, a confirmed report back, a stopped
/// project's run state and the cards the owner stopped by hand. The shell calls it with the lifecycles.
/// </summary>
public static class CardStopReasons
{
    /// <summary>The reason for one card, or null.</summary>
    /// <param name="card">The card.</param>
    /// <param name="runStates">Each stopped project's run state, by project name.</param>
    /// <param name="projectTitle">A project's title from its name.</param>
    /// <param name="stoppedByOwner">The cards the owner stopped by hand while their project ran.</param>
    /// <param name="links">Every link, for its confirmed report back.</param>
    /// <param name="cardTitle">A card's title from its key, for "Paused by <card>"; null shows the key.</param>
    public static WhyStopped? For(WorkstreamCardViewModel card, IReadOnlyDictionary<string, IReadOnlyList<string>> runStates,
        Func<string, string> projectTitle, IReadOnlyCollection<string> stoppedByOwner, IReadOnlyList<Link> links,
        Func<string, string>? cardTitle = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(runStates);
        ArgumentNullException.ThrowIfNull(projectTitle);
        ArgumentNullException.ThrowIfNull(stoppedByOwner);
        ArgumentNullException.ThrowIfNull(links);

        var confirmed = links
            .Where(link => link is { IsActive: true, Kind: LinkKind.ReportsBack, SatisfiedBy: LinkBook.ConfirmedReport, SatisfiedUtc: not null }
                           && string.Equals(link.From, card.Key, StringComparison.OrdinalIgnoreCase))
            .Max(link => link.SatisfiedUtc);
        var stoppedIn = runStates.FirstOrDefault(state => state.Value.Contains(card.Key, StringComparer.OrdinalIgnoreCase)).Key;

        return WhyStopped.For(card.IsLive, card.IsInLastStage, confirmed, stoppedIn is null ? null : projectTitle(stoppedIn),
            stoppedByOwner.Contains(card.Key, StringComparer.OrdinalIgnoreCase),
            card.Workstream.PausedBy is { } by ? cardTitle?.Invoke(by) ?? by : null, card.Workstream.PauseReason);
    }
}
