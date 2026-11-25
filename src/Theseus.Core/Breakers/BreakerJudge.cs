using Theseus.Core.Running;

namespace Theseus.Core.Breakers;

/// <summary>
/// Reads a card's runs since its last reset and says whether a breaker trips. Only the owner's Reset clears a trip, and
/// a reset starts the count again, which is why the caller passes only the runs since then.
/// </summary>
public static class BreakerJudge
{
    /// <summary>The breaker that trips after the latest run, or null.</summary>
    /// <param name="runs">The card's runs since its last reset, in any order.</param>
    /// <param name="settings">The thresholds.</param>
    /// <param name="now">Now, for the trip's time.</param>
    public static BreakerTrip? After(IEnumerable<RunRecord> runs, BreakerSettings settings, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(runs);
        ArgumentNullException.ThrowIfNull(settings);

        var recent = runs.Where(run => !run.IsHandoffRun).OrderByDescending(run => run.EndedUtc).ToList();

        if (recent.Count == 0)
        {
            return null;
        }

        var last = recent[0];

        if (last.Outcome == RunOutcome.OverTime)
        {
            return new BreakerTrip(BreakerKind.OverTime,
                $"Breaker tripped: a run was still going after {Minutes(last.EndedUtc - last.StartedUtc)}, past its time limit and its grace, and was interrupted.",
                now);
        }

        if (InARow(recent, settings.Crashes, run => run.Outcome is RunOutcome.Crashed or RunOutcome.FailedStart))
        {
            return new BreakerTrip(BreakerKind.Crashes,
                $"Breaker tripped: {settings.Crashes} crashes or failed starts in a row{Last(last)}.", now);
        }

        if (last.Error is { Length: > 0 } error
            && InARow(recent, settings.SameError, run => string.Equals(run.Error, error, StringComparison.Ordinal)))
        {
            return new BreakerTrip(BreakerKind.SameError,
                $"Breaker tripped: the same error {settings.SameError} runs in a row: {error}", now);
        }

        if (InARow(recent, settings.NoProgress, run => !run.MadeProgress))
        {
            return new BreakerTrip(BreakerKind.NoProgress,
                $"Breaker tripped: {settings.NoProgress} runs in a row with no new commit, record, tick or report.", now);
        }

        return null;
    }

    private static bool InARow(List<RunRecord> newestFirst, int count, Func<RunRecord, bool> test) =>
        count > 0 && newestFirst.Count >= count && newestFirst.Take(count).All(test);

    private static string Last(RunRecord run) => run.Error is { Length: > 0 } error ? $" (last: {error})" : string.Empty;

    private static string Minutes(TimeSpan span) => $"{(int)span.TotalMinutes} minutes";
}
