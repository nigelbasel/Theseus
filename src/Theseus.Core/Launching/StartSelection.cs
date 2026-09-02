using Theseus.Core.Domain;
using Theseus.Core.Sessions;

namespace Theseus.Core.Launching;

/// <summary>
/// Decides which workstreams a Start action actually starts. Starting several at once is the one
/// gesture with real cost, so the rule is here, in one tested place, rather than spread across the
/// board.
///
/// **Nothing is inferred from where a card sits or from what its column is called.** A whole-board
/// Start used to launch every lane but the last, on the reasoning that the last lane holds finished
/// work. That is a rule about position, and it was wrong the moment a board had four stages instead
/// of three: `New / In progress / Paused / Done` puts `Paused` second-from-last, so a single click
/// restarted everything its owner had deliberately paused. Reading the *name* would have been worse
/// — the columns are the user's to name, in any language, and a guess about "Paused" or "Parked" is
/// a guess about English.
///
/// So the board no longer guesses. The user ticks the cards they mean, and this starts those of them
/// that can be started.
/// </summary>
public static class StartSelection
{
    /// <summary>
    /// The workstreams a Start should launch, from the cards the user chose. A selection spans
    /// several stages, so the order runs across the board before down it: what is furthest along
    /// starts first, which is the order the morning wants.
    /// </summary>
    public static IReadOnlyList<Workstream> ForCards(
        IReadOnlyList<Workstream> workstreams,
        IReadOnlyList<string> lanes,
        SessionRegistrySnapshot live)
    {
        ArgumentNullException.ThrowIfNull(workstreams);
        ArgumentNullException.ThrowIfNull(lanes);

        return Eligible(workstreams, lanes, live);
    }

    /// <summary>Whether one workstream can be started right now.</summary>
    public static bool IsEligible(Workstream workstream, SessionRegistrySnapshot live)
    {
        ArgumentNullException.ThrowIfNull(workstream);
        ArgumentNullException.ThrowIfNull(live);

        if (workstream.IsArchived || string.IsNullOrWhiteSpace(workstream.Prompt))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(workstream.LastSessionId)
               || live.WithSessionId(workstream.LastSessionId).Count == 0;
    }

    private static IReadOnlyList<Workstream> Eligible(
        IEnumerable<Workstream> workstreams,
        IReadOnlyList<string> lanes,
        SessionRegistrySnapshot live)
    {
        ArgumentNullException.ThrowIfNull(live);

        return workstreams
            .Where(workstream => IsEligible(workstream, live))
            .OrderBy(workstream => LanePosition(workstream.Column, lanes))
            .ThenBy(workstream => workstream.BoardOrder)
            .ToList();
    }

    private static int LanePosition(string? column, IReadOnlyList<string> lanes)
    {
        var resolved = BoardLanes.Resolve(column, lanes);
        var index = lanes.ToList().FindIndex(lane => string.Equals(lane, resolved, StringComparison.Ordinal));

        return index < 0 ? lanes.Count : index;
    }
}
