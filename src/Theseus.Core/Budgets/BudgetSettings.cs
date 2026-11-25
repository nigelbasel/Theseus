using System.Text.Json.Serialization;

namespace Theseus.Core.Budgets;

/// <summary>
/// The limits a headless run works inside. Each is a setting with a stated default, and a project can replace them with
/// its own (Nigel, 2026-11-24). The figures are starting values, to be revised from the run ledger.
/// </summary>
/// <param name="RunCostLimitUsd">Cost per run, in US dollars.</param>
/// <param name="RunTokenLimit">Tokens per run: input, output and cache creation. Cache reads are shown but not counted.</param>
/// <param name="RunTimeLimitMinutes">Wall-clock per run.</param>
/// <param name="CardDayCostLimitUsd">
/// Cost per card per local calendar day, or null for none: off unless set (Nigel, 2026-11-25). Stored under a new name so
/// the old default a file holds only because it was the default is told apart by <see cref="Settings.DayLimitUpgrade"/>.
/// </param>
/// <param name="ProjectDayCostLimitUsd">Cost per project per local calendar day, or null for none, as the card's.</param>
/// <param name="StopAtPercent">The share of a run's token or time limit at which the run is asked to hand over and stop.</param>
/// <param name="HandoffAllowanceUsd">
/// What the one resume after a hard cost stop may spend: Claude Code's own cap ends a run without a handoff, so the run
/// is resumed once, told only to write it.
/// </param>
public sealed record BudgetSettings(
    decimal RunCostLimitUsd = 5.00m,
    long RunTokenLimit = 3_000_000,
    int RunTimeLimitMinutes = 120,
    [property: JsonPropertyName("cardDayLimitUsd")] decimal? CardDayCostLimitUsd = null,
    [property: JsonPropertyName("projectDayLimitUsd")] decimal? ProjectDayCostLimitUsd = null,
    int StopAtPercent = 90,
    decimal HandoffAllowanceUsd = 1.00m)
{
    /// <summary>The defaults a first run starts from.</summary>
    public static BudgetSettings Default { get; } = new();
}
