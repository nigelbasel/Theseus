using Theseus.Core.Sessions;

namespace Theseus.Core.Domain;

/// <summary>
/// What a card should say about its session. A session that has exited is not the same as one that
/// was never started: the board is a record of a day's work, and a workstream that ran and finished
/// has to keep saying so once its tab has gone.
/// </summary>
public static class CardLifecycle
{
    /// <summary>
    /// The state of one workstream's card. A live session outranks the history; otherwise the run
    /// log decides, because the registry entry is deleted the moment a session exits cleanly and
    /// nothing else on disk remembers that the workstream ran at all.
    /// <para>
    /// The moment of ending is often unknown: the run log is append-only, so a run whose ending was
    /// never recorded has none, and the card is told nothing rather than told the start time.
    /// </para>
    /// </summary>
    /// <param name="reportedStatus">
    /// What a live session says it is doing, from the registry. Null when nothing is running.
    /// </param>
    public static CardState For(
        string workstreamKey,
        string? reportedStatus,
        bool isLive,
        IReadOnlyList<SessionRun> runs,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(runs);

        if (isLive)
        {
            return new CardState(SessionActivity.For(reportedStatus), null);
        }

        var latest = LatestToday(workstreamKey, runs, now);

        return latest is null
            ? new CardState(SessionLifecycle.NotStarted, null)
            : new CardState(SessionLifecycle.Ended, latest.EndedUtc);
    }

    private static SessionRun? LatestToday(
        string workstreamKey,
        IReadOnlyList<SessionRun> runs,
        DateTimeOffset now) =>
        runs
            .Where(run => string.Equals(run.WorkstreamKey, workstreamKey, StringComparison.OrdinalIgnoreCase))
            .Where(run => RanToday(run, now))
            .MaxBy(run => run.StartedUtc);

    /// <summary>
    /// Whether a run was under way at some point today, which is not the same as having begun today.
    /// A session left working overnight belongs to this morning as much as to last night, and a card
    /// that asks only when the run started calls that workstream untouched while its notes from a few
    /// hours ago sit on disk.
    ///
    /// A run that ended without its moment being observed cannot answer the question, so it falls
    /// back to when it started rather than claiming a day it may have had nothing to do with. A run
    /// still going is not this method's business: a live session is settled before the history is
    /// consulted at all, and one that died unobserved is given an ending by the endings sweep, which
    /// has already run by the time a card is asked what to say.
    /// </summary>
    private static bool RanToday(SessionRun run, DateTimeOffset now) =>
        IsToday(run.StartedUtc, now)
        || (run.EndedUtc is { } ended && ended.ToLocalTime().Date >= now.ToLocalTime().Date);

    private static bool IsToday(DateTimeOffset moment, DateTimeOffset now) =>
        moment.ToLocalTime().Date == now.ToLocalTime().Date;
}
