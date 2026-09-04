using Theseus.Core.Domain;

namespace Theseus.Core.Reporting;

/// <summary>
/// What was worked on in one day, from the run log.
///
/// Theseus already records when every session started and ended, so this asks that record a question
/// rather than keeping a second one. It exists because time logged into Jira by hand is unreliable,
/// and a permanent file per day is worth more than a figure on a screen.
///
/// Two decisions shape it, both taken because the alternative would have been a confident wrong
/// number:
///
/// **Time is clipped to the day.** A session running from last night until this afternoon belongs to
/// both days; giving either the whole of it would be wrong twice over. The same overlap thinking that
/// fixed "not started today" on the cards.
///
/// **Time is per workstream, and issues are listed beside it.** The run log knows which workstream a
/// session was, and nothing knows how to divide a session between the sixteen issues one card here
/// carries. Apportioning would invent the number this report exists to replace.
/// </summary>
public static class DayReport
{
    /// <summary>Everything worked on during one local day, most time first.</summary>
    /// <param name="day">The day, in local time.</param>
    /// <param name="runs">Every run ever recorded; those touching the day are picked out.</param>
    /// <param name="workstreams">The board, for titles, areas and issues.</param>
    /// <param name="handedOverToday">
    /// Whether a workstream's handover document was written today. Passed in because answering it
    /// reads the disk, and this has to stay arithmetic.
    /// </param>
    /// <param name="now">The present moment, which bounds a session that has not ended.</param>
    /// <param name="isLive">
    /// Whether a session is running right now, asked by its identifier. The run log is not the
    /// authority on this: it holds runs whose endings were never observed, going back weeks, and
    /// treating those as still running attributes hours of today to work that finished long ago.
    /// </param>
    public static IReadOnlyList<DayReportEntry> For(
        DateOnly day,
        IReadOnlyList<SessionRun> runs,
        IReadOnlyList<Workstream> workstreams,
        Func<string, bool> handedOverToday,
        Func<string, bool> isLive,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(workstreams);
        ArgumentNullException.ThrowIfNull(handedOverToday);
        ArgumentNullException.ThrowIfNull(isLive);

        var from = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), now.Offset);
        var to = from.AddDays(1);

        var byKey = workstreams
            .GroupBy(workstream => workstream.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return
        [
            .. Closed(runs, now.Offset)
                .Select(run => (run, worked: Overlap(run, from, to, now, isLive)))
                .Where(pair => pair.worked is null || pair.worked > TimeSpan.Zero)
                .GroupBy(pair => pair.run.WorkstreamKey, StringComparer.OrdinalIgnoreCase)
                .Select(group => Entry(group.Key, group, byKey, handedOverToday, isLive))
                .Where(entry => entry.Sessions > 0 || entry.UnmeasuredSessions > 0)
                .OrderByDescending(entry => entry.Worked)
                .ThenBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
        ];
    }

    /// <summary>
    /// The tabs that can be closed: workstreams that were worked on today and are no longer running.
    /// Said plainly because it is the one part of the report that prompts an action.
    /// </summary>
    public static IReadOnlyList<DayReportEntry> SafeToClose(IReadOnlyList<DayReportEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return [.. entries.Where(entry => !entry.StillRunning)];
    }

    /// <summary>How long the day came to in total.</summary>
    public static TimeSpan Total(IReadOnlyList<DayReportEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries.Aggregate(TimeSpan.Zero, (running, entry) => running + entry.Worked);
    }

    /// <summary>
    /// The runs with the endings that can be inferred filled in.
    ///
    /// Resuming a session reuses its identifier, so one session id covers several runs — six, for one
    /// workstream here. Only the last of them can still be going; each earlier one had ended by the
    /// time the next began, whether or not anybody recorded it. Without this every one of the six was
    /// treated as running now, and a single workstream was credited with sixty-two hours of one day.
    /// </summary>
    /// <param name="offset">
    /// The local offset, because "the same day" is a local question.
    ///
    /// A run followed by another under the same identifier has ended - only the latest can still be
    /// going. If the next began the same day, the earlier one ended when it started. If the next
    /// began on a later day, the gap between them is not a session but an ending nobody saw, so the
    /// earlier run is ended at the midnight after it started: the rest of its own day as an upper
    /// bound, and nothing on any day after. Bounding it at the resume instead credited one day with
    /// eighteen hours of a session that had been dead for a week; leaving it open was no better,
    /// because its identifier is the live one and it was then bounded at the present moment.
    /// </param>
    private static IEnumerable<SessionRun> Closed(IReadOnlyList<SessionRun> runs, TimeSpan offset) =>
        runs.GroupBy(run => run.ClaudeSessionId, StringComparer.OrdinalIgnoreCase)
            .SelectMany(session =>
            {
                var ordered = session.OrderBy(run => run.StartedUtc).ToList();

                return ordered.Select((run, index) =>
                {
                    if (run.EndedUtc is not null || index + 1 >= ordered.Count)
                    {
                        return run;
                    }

                    var next = ordered[index + 1].StartedUtc;

                    return run with
                    {
                        EndedUtc = SameLocalDay(run.StartedUtc, next, offset)
                            ? next
                            : MidnightAfter(run.StartedUtc, offset),
                    };
                });
            });

    private static bool SameLocalDay(DateTimeOffset first, DateTimeOffset second, TimeSpan offset) =>
        first.ToOffset(offset).Date == second.ToOffset(offset).Date;

    private static DateTimeOffset MidnightAfter(DateTimeOffset moment, TimeSpan offset) =>
        new(moment.ToOffset(offset).Date.AddDays(1), offset);

    /// <summary>
    /// How much of one run fell inside the day, or null when that cannot be known.
    ///
    /// A run with no recorded ending is one of two very different things. If its session is running
    /// now, it has been going since it started and the day gets the part that falls inside it. If it
    /// is not, nobody ever saw it end — this log holds 112 such runs, some three weeks old — and
    /// bounding those at the present moment credits today with hours that belong to another week.
    /// Counted that way, one real day came to 287 hours.
    /// </summary>
    private static TimeSpan? Overlap(
        SessionRun run, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now, Func<string, bool> isLive)
    {
        if (run.EndedUtc is null && !isLive(run.ClaudeSessionId))
        {
            // It touched the day only if it began before the day ended; whether it ran for a minute
            // of it or all of it is exactly what cannot be told.
            return run.StartedUtc < to ? null : TimeSpan.Zero;
        }

        var ended = run.EndedUtc ?? now;
        var overlap = (ended < to ? ended : to) - (run.StartedUtc > from ? run.StartedUtc : from);

        return overlap > TimeSpan.Zero ? overlap : TimeSpan.Zero;
    }

    private static DayReportEntry Entry(
        string key,
        IEnumerable<(SessionRun Run, TimeSpan? Worked)> runs,
        IReadOnlyDictionary<string, Workstream> byKey,
        Func<string, bool> handedOverToday,
        Func<string, bool> isLive)
    {
        var all = runs.ToList();
        var measured = all.Where(pair => pair.Worked is { } worked && worked > TimeSpan.Zero).ToList();
        var workstream = byKey.GetValueOrDefault(key);

        return new DayReportEntry(
            key,
            workstream?.Title ?? all[0].Run.TabTitle ?? key,
            workstream?.Area ?? string.Empty,
            measured.Aggregate(TimeSpan.Zero, (running, pair) => running + pair.Worked!.Value),
            measured.Count,
            all.Any(pair => isLive(pair.Run.ClaudeSessionId)),
            workstream?.JiraKeys ?? [],
            handedOverToday(key),
            all.Count(pair => pair.Worked is null));
    }
}
