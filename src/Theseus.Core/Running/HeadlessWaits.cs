using Theseus.Core.Domain;
using Theseus.Core.Projects;

namespace Theseus.Core.Running;

/// <summary>
/// What a project's headless cards wait on the owner for: a tripped breaker, which only his Reset clears, and a run
/// stopped at a budget limit, which he can raise (Nigel, 2026-11-24).
/// </summary>
public static class HeadlessWaits
{
    /// <summary>The waits, one per card at most.</summary>
    /// <param name="cards">The project's cards.</param>
    /// <param name="stateOf">A card's headless state, by key, or null.</param>
    public static IReadOnlyList<WaitItem> For(IEnumerable<Workstream> cards, Func<string, HeadlessCardState?> stateOf)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(stateOf);

        var waits = new List<WaitItem>();

        foreach (var card in cards)
        {
            if (card.Breaker is { } trip)
            {
                waits.Add(new WaitItem(WaitKind.ResetBreaker, $"{card.Title}: {trip.Detail} Reset it from the card when fixed.", [])
                {
                    CardKey = card.Key,
                });

                continue;
            }

            if (stateOf(card.Key) is { IsRunning: false, QueuePosition: null, Last: { Outcome: RunOutcome.StoppedAtLimit, IsHandoffRun: false } last })
            {
                waits.Add(new WaitItem(WaitKind.RaiseBudget,
                    $"{card.Title}: {last.LimitDetail ?? "stopped at a limit"} Raise it in Options › General › Headless runs, then start it.", [])
                {
                    CardKey = card.Key,
                });
            }
        }

        return waits;
    }
}
