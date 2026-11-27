using Theseus.Core.Running;

namespace Theseus.Core.Attention;

/// <summary>
/// Machine time with the ledger's headless runs merged in, overlaps removed (7.3). The sampler saw 5.9 h of the ledger's
/// 20.5 h of headless runs, because it counted a card's last session only; the ledger knows when every run started and
/// ended. A run's time already sampled, on its own session or, on a line from before sessions were stamped, on its card,
/// is not counted twice. Merged when read: nothing is written back.
/// </summary>
public static class MachineTime
{
    /// <summary>What a run's interval is marked with, so a report can tell merged time from sampled.</summary>
    public const string LedgerSource = "ledger";

    /// <summary>The intervals with every ledger run's unsampled time added as machine time.</summary>
    public static IReadOnlyList<AttentionInterval> Merge(IReadOnlyList<AttentionInterval> intervals, IReadOnlyList<RunRecord> runs)
    {
        ArgumentNullException.ThrowIfNull(intervals);
        ArgumentNullException.ThrowIfNull(runs);

        if (runs.Count == 0)
        {
            return intervals;
        }

        var machine = intervals.Where(interval => interval.Kind == AttentionKind.Machine).ToList();
        var bySession = machine.Where(interval => interval.Session is not null)
            .ToLookup(interval => interval.Session!, StringComparer.OrdinalIgnoreCase);
        var byCardUnstamped = machine.Where(interval => interval.Session is null)
            .ToLookup(interval => interval.Workstream, StringComparer.OrdinalIgnoreCase);
        var merged = intervals.ToList();

        foreach (var run in runs.Where(run => run.EndedUtc > run.StartedUtc))
        {
            var covered = bySession[run.SessionId].Concat(byCardUnstamped[run.Workstream])
                .Select(interval => (interval.Start, interval.End));

            merged.AddRange(Spans.Without((run.StartedUtc, run.EndedUtc), covered)
                .Select(span => new AttentionInterval(AttentionKind.Machine, run.Workstream, span.Start, span.End)
                {
                    Session = run.SessionId,
                    Project = run.Project,
                    Source = LedgerSource,
                }));
        }

        return merged;
    }
}
