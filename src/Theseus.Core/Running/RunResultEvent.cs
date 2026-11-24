namespace Theseus.Core.Running;

/// <summary>A turn ended: Claude Code's <c>result</c> line, the only one that carries cost.</summary>
/// <param name="TotalCostUsd">The process's cost so far: it is cumulative across the run's turns.</param>
/// <param name="Usage">The turn's tokens.</param>
/// <param name="Subtype"><c>success</c>, <c>error_max_budget_usd</c>, or another error subtype.</param>
/// <param name="IsError">Whether the turn ended in an error.</param>
/// <param name="Text">The turn's result text, or the error.</param>
public sealed record RunResultEvent(decimal? TotalCostUsd, RunTokens Usage, string Subtype, bool IsError, string? Text) : RunStreamEvent
{
    /// <summary>The subtype Claude Code gives when its own <c>--max-budget-usd</c> cap ended the run.</summary>
    public const string MaxBudgetSubtype = "error_max_budget_usd";

    /// <summary>Whether Claude Code's own cost cap ended the run.</summary>
    public bool HitCostCap => string.Equals(Subtype, MaxBudgetSubtype, StringComparison.Ordinal);
}
