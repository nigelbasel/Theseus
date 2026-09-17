namespace Theseus.Core.Attention;

/// <summary>
/// Puts the time the sampler measured against the issues sessions said they were working on.
///
/// Sessions mark stretches - "on NDC-680 from now" - and never durations. Within a workstream, the
/// issue at any moment is the one most recently started by any of its sessions that has not since
/// stopped or moved on; the attended and machine time the sampler recorded for that workstream in
/// that moment belongs to that issue. So time is only ever given to one issue per workstream at once,
/// and attended time stays within the wall clock however many sessions mark work in parallel.
///
/// A stretch nobody stopped runs until now. That over-states nothing the sampler did not measure:
/// attended time still needs the tab in front, and machine time still needs the session working.
/// </summary>
public static class IssueTimeCalculator
{
    /// <summary>The time on one issue between two moments.</summary>
    public static IssueTime For(
        string issue,
        IReadOnlyList<WorkMark> marks,
        IReadOnlyList<AttentionInterval> intervals,
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issue);
        ArgumentNullException.ThrowIfNull(marks);
        ArgumentNullException.ThrowIfNull(intervals);

        var attended = TimeSpan.Zero;
        var machine = TimeSpan.Zero;
        var workstreams = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var workstream in marks.GroupBy(mark => mark.Workstream, StringComparer.OrdinalIgnoreCase))
        {
            var spans = Spans([.. workstream.OrderBy(mark => mark.At)], issue, now)
                .Select(span => (Start: Max(span.Start, from), End: Min(span.End, to)))
                .Where(span => span.End > span.Start)
                .ToList();

            if (spans.Count == 0)
            {
                continue;
            }

            workstreams.Add(workstream.Key);

            foreach (var interval in intervals.Where(interval =>
                         string.Equals(interval.Workstream, workstream.Key, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var span in spans)
                {
                    var overlap = Min(span.End, interval.End) - Max(span.Start, interval.Start);

                    if (overlap <= TimeSpan.Zero)
                    {
                        continue;
                    }

                    if (interval.Kind == AttentionKind.Attended)
                    {
                        attended += overlap;
                    }
                    else
                    {
                        machine += overlap;
                    }
                }
            }
        }

        return new IssueTime(issue, from, to, attended, machine, [.. workstreams]);
    }

    private static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> Spans(
        IReadOnlyList<WorkMark> marks, string issue, DateTimeOffset now)
    {
        var active = new Dictionary<string, (string Issue, DateTimeOffset Since)>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < marks.Count; index++)
        {
            var mark = marks[index];

            if (mark.Kind == WorkMarkKind.Start && !string.IsNullOrWhiteSpace(mark.Issue))
            {
                active[mark.SessionId] = (mark.Issue, mark.At);
            }
            else
            {
                active.Remove(mark.SessionId);
            }

            var current = active.Values.OrderByDescending(entry => entry.Since).Select(entry => entry.Issue).FirstOrDefault();
            var end = index + 1 < marks.Count ? marks[index + 1].At : now;

            if (end > mark.At && string.Equals(current, issue, StringComparison.OrdinalIgnoreCase))
            {
                yield return (mark.At, end);
            }
        }
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left > right ? left : right;

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;
}
