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
    /// </summary>
    public static CardState For(
        string workstreamKey,
        bool isLive,
        IReadOnlyList<SessionRun> runs,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(runs);

        if (isLive)
        {
            return new CardState(SessionLifecycle.Running, null);
        }

        var latest = LatestToday(workstreamKey, runs, now);

        return latest is null
            ? new CardState(SessionLifecycle.NotStarted, null)
            : new CardState(SessionLifecycle.Ended, latest.EndedUtc ?? latest.StartedUtc);
    }

    private static SessionRun? LatestToday(
        string workstreamKey,
        IReadOnlyList<SessionRun> runs,
        DateTimeOffset now) =>
        runs
            .Where(run => string.Equals(run.WorkstreamKey, workstreamKey, StringComparison.OrdinalIgnoreCase))
            .Where(run => IsToday(run.StartedUtc, now))
            .MaxBy(run => run.StartedUtc);

    private static bool IsToday(DateTimeOffset moment, DateTimeOffset now) =>
        moment.ToLocalTime().Date == now.ToLocalTime().Date;
}
