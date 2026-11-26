namespace Theseus.Core.Budgets;

/// <summary>
/// The limits one run starts with: the least of the per-run limits and what is left of its card's and project's day,
/// with the limit that set the cost cap, so a stop names the right one.
/// </summary>
/// <param name="CostUsd">The cost cap, given to Claude Code as <c>--max-budget-usd</c>, or null for none.</param>
/// <param name="Tokens">The token limit, watched live, or null for none.</param>
/// <param name="Time">The wall-clock limit, watched live, or null for none: with none, a run is never over time.</param>
/// <param name="CostLimit">Which limit the cost cap comes from.</param>
/// <param name="CostLimitValue">The full value of that limit, for the card's sentence, or null when there is no cap.</param>
/// <param name="StopAtPercent">The share of the token and time limits at which the stop is asked for.</param>
public sealed record RunLimits(
    decimal? CostUsd,
    long? Tokens,
    TimeSpan? Time,
    BudgetLimit CostLimit,
    decimal? CostLimitValue,
    int StopAtPercent);
