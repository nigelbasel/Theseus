namespace Theseus.Core.Attention;

/// <summary>
/// Jira starts nobody stopped, stopped by Theseus (7.3). 25 cards had a start that was never stopped, so every later
/// minute on the card went to an old issue. A session's start is stale when its card has had no attended or machine time
/// for <see cref="IdleFor"/>; the stop is dated at the card's last measured moment after the start, so no time that was
/// measured moves, and it is marked <see cref="Summary"/> so it is never taken for the session's own.
/// </summary>
public static class StaleStarts
{
    /// <summary>How long a card must have had no time before its open starts are stopped.</summary>
    public static readonly TimeSpan IdleFor = TimeSpan.FromHours(8);

    /// <summary>What an automatic stop says.</summary>
    public const string Summary = "auto: idle";

    /// <summary>The stops due now, one per session whose last mark is a stale start.</summary>
    /// <param name="marks">The work log, in the order written.</param>
    /// <param name="intervals">The measured time.</param>
    /// <param name="now">The time now.</param>
    public static IReadOnlyList<WorkMark> Due(IReadOnlyList<WorkMark> marks, IReadOnlyList<AttentionInterval> intervals, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(intervals);

        var open = marks
            .GroupBy(mark => mark.SessionId, StringComparer.OrdinalIgnoreCase)
            .Select(session => session.Last())
            .Where(mark => mark is { Kind: WorkMarkKind.Start })
            .ToList();

        if (open.Count == 0)
        {
            return [];
        }

        var lastSeen = intervals
            .GroupBy(interval => interval.Workstream, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(card => card.Key, card => card.Max(interval => interval.End), StringComparer.OrdinalIgnoreCase);

        var due = new List<WorkMark>();

        foreach (var start in open)
        {
            var last = lastSeen.TryGetValue(start.Workstream, out var seen) && seen > start.At ? seen : start.At;

            if (now - last >= IdleFor)
            {
                due.Add(new WorkMark(start.SessionId, start.Workstream, WorkMarkKind.Stop, null, Summary, last));
            }
        }

        return due;
    }
}
