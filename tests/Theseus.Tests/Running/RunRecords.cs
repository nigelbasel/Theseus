using Theseus.Core.Running;

namespace Theseus.Tests.Running;

/// <summary>Ledger records for the budget and breaker tests.</summary>
internal static class RunRecords
{
    public static readonly DateTimeOffset Noon = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    public static RunRecord Run(
        string workstream = "card",
        string? project = "proj",
        decimal? cost = 1m,
        RunOutcome outcome = RunOutcome.Completed,
        DateTimeOffset? ended = null,
        bool progress = true,
        string? error = null,
        bool handoff = false) =>
        new(Guid.NewGuid(), workstream, project, "session", RunReason.Owner, (ended ?? Noon).AddMinutes(-10), ended ?? Noon,
            outcome, cost, RunTokens.None)
        {
            MadeProgress = progress,
            Error = error,
            IsHandoffRun = handoff,
        };
}
