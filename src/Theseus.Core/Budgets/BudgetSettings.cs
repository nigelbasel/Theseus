using System.Text.Json.Serialization;

namespace Theseus.Core.Budgets;

/// <summary>
/// The limits a headless run works inside. Each is a setting, and a project can replace them with its own (Nigel,
/// 2026-11-24). Every cost, token and time limit is off unless set (Nigel, 2026-11-25: "we dont want to have any limits
/// turned on"). Each is stored under a newer name than its old default was, so <see cref="Settings.LimitUpgrade"/> can tell
/// a figure someone chose from one a file holds only because the whole budget is written back.
/// </summary>
/// <param name="RunCostLimitUsd">Cost per run, in US dollars, or null for none.</param>
/// <param name="RunTokenLimit">
/// Tokens per run, or null for none: input, output and cache creation. Cache reads are shown but not counted.
/// </param>
/// <param name="RunTimeLimitMinutes">Wall-clock per run, or null for none; with none, the over-time breaker never trips.</param>
/// <param name="CardDayCostLimitUsd">
/// Cost per card per local calendar day, or null for none.
/// </param>
/// <param name="ProjectDayCostLimitUsd">Cost per project per local calendar day, or null for none, as the card's.</param>
/// <param name="StopAtPercent">The share of a run's token or time limit at which the run is asked to hand over and stop.</param>
/// <param name="HandoffAllowanceUsd">
/// What the one resume after a hard cost stop may spend: Claude Code's own cap ends a run without a handoff, so the run
/// is resumed once, told only to write it.
/// </param>
public sealed record BudgetSettings(
    [property: JsonPropertyName("runLimitUsd")] decimal? RunCostLimitUsd = null,
    [property: JsonPropertyName("runLimitTokens")] long? RunTokenLimit = null,
    [property: JsonPropertyName("runLimitMinutes")] int? RunTimeLimitMinutes = null,
    [property: JsonPropertyName("cardDayLimitUsd")] decimal? CardDayCostLimitUsd = null,
    [property: JsonPropertyName("projectDayLimitUsd")] decimal? ProjectDayCostLimitUsd = null,
    int StopAtPercent = 90,
    decimal HandoffAllowanceUsd = 1.00m)
{
    /// <summary>The defaults a first run starts from.</summary>
    public static BudgetSettings Default { get; } = new();
}
