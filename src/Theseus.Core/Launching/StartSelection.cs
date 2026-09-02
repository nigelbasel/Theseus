using Theseus.Core.Domain;
using Theseus.Core.Sessions;

namespace Theseus.Core.Launching;

/// <summary>
/// Decides which workstreams a Start action actually starts. Starting everything is the one gesture
/// with real cost, so the rules are here, in one tested place, rather than spread across the board.
/// </summary>
public static class StartSelection
{
    /// <summary>
    /// The workstreams a whole-board Start should launch: every lane except the last, which is
    /// where finished work lives and must never be restarted by a single click.
    /// </summary>
    public static IReadOnlyList<Workstream> ForBoard(
        IReadOnlyList<Workstream> workstreams,
        IReadOnlyList<string> lanes,
        SessionRegistrySnapshot live)
    {
        ArgumentNullException.ThrowIfNull(workstreams);
        ArgumentNullException.ThrowIfNull(lanes);

        if (lanes.Count == 0)
        {
            return [];
        }

        return Eligible(
            workstreams.Where(workstream => !BoardLanes.IsLast(workstream.Column, lanes)),
            lanes,
            live);
    }

    /// <summary>The workstreams a single lane's Start should launch.</summary>
    public static IReadOnlyList<Workstream> ForColumn(
        IReadOnlyList<Workstream> workstreams,
        string lane,
        SessionRegistrySnapshot live)
    {
        ArgumentNullException.ThrowIfNull(workstreams);

        return Eligible(
            workstreams.Where(workstream => BoardLanes.Same(lane, workstream.Column)),
            [lane],
            live);
    }

    /// <summary>
    /// The workstreams a Start on some part of the grid should launch, from the cards that part
    /// holds. A row spans several stages, so the order still runs across the board before down it:
    /// what is furthest along starts first, which is the order the morning wants.
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
