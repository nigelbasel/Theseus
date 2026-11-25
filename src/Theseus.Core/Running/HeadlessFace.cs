using System.Globalization;
using Theseus.Core.Budgets;
using Theseus.Core.Sessions;

namespace Theseus.Core.Running;

/// <summary>The line a headless card's face shows: going, queued or ended, with its measured tokens and cost.</summary>
public static class HeadlessFace
{
    /// <summary>The line, or null for a card that has had no headless run.</summary>
    public static string? For(HeadlessCardState? state, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (state is null)
        {
            return null;
        }

        if (state.IsRunning && state.StartedUtc is { } started)
        {
            return Join("Headless", $"running {Elapsed(now - started)}", Tokens(state.Tokens), Cost(state.CostUsd));
        }

        if (state.QueuePosition is { } position)
        {
            return $"Headless · queued ({Ordinal(position)})";
        }

        if (state.Last is not { } last)
        {
            return null;
        }

        var ended = TimeZoneInfo.ConvertTime(last.EndedUtc, zone).ToString("HH:mm", CultureInfo.InvariantCulture);

        return Join("Headless", $"ended {ended}", Cost(last.CostUsd) ?? "cost not reported", Tokens(last.Tokens), Outcome(last));
    }

    private static string? Outcome(RunRecord last) => last.Outcome switch
    {
        RunOutcome.Completed => null,
        RunOutcome.StoppedAtLimit => last.LimitDetail ?? "stopped at its limit",
        RunOutcome.Crashed => $"crashed: {last.Error}",
        RunOutcome.FailedStart => $"did not start: {last.Error}",
        RunOutcome.Failed => $"failed: {last.Error}",
        RunOutcome.OverTime => "interrupted: over its time limit",
        RunOutcome.Interrupted => "opened in a tab",
        _ => null,
    };

    private static string? Tokens(RunTokens tokens) => tokens.Counted == 0 ? null : $"{TokenCount.Format(tokens.Counted)} tokens";

    private static string? Cost(decimal? cost) => cost is { } amount ? Money.Usd(amount) : null;

    private static string Elapsed(TimeSpan span) =>
        span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m" : $"{Math.Max(0, (int)span.TotalMinutes)}m";

    private static string Ordinal(int position) => position switch
    {
        1 => "next",
        2 => "2nd",
        3 => "3rd",
        _ => $"{position}th",
    };

    private static string Join(params string?[] parts) => string.Join(" · ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
}
