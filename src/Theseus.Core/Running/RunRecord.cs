using Theseus.Core.Budgets;

namespace Theseus.Core.Running;

/// <summary>
/// One ended headless run, as the run ledger keeps it: what it cost, measured from its own output and never estimated,
/// how it ended, and whether it made progress, which is what the budgets and the circuit breakers read (Nigel, 2026-11-24).
/// </summary>
/// <param name="RunId">The run's identifier.</param>
/// <param name="Workstream">The card's key.</param>
/// <param name="Project">The card's project, or null.</param>
/// <param name="SessionId">The Claude Code session it ran under.</param>
/// <param name="Reason">What asked for it.</param>
/// <param name="StartedUtc">When it started.</param>
/// <param name="EndedUtc">When its process exited.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="CostUsd">Its cost from its last result, or null when it produced none.</param>
/// <param name="Tokens">Its tokens, summed over its results.</param>
public sealed record RunRecord(
    Guid RunId,
    string Workstream,
    string? Project,
    string SessionId,
    RunReason Reason,
    DateTimeOffset StartedUtc,
    DateTimeOffset EndedUtc,
    RunOutcome Outcome,
    decimal? CostUsd,
    RunTokens Tokens)
{
    /// <summary>The project's piece of work the card was on, when known, so cost can be told per piece.</summary>
    public string? Piece { get; init; }

    /// <summary>The limit it stopped at, when its outcome is <see cref="RunOutcome.StoppedAtLimit"/>.</summary>
    public BudgetLimit? Limit { get; init; }

    /// <summary>The card's sentence for the limit: measured against limit.</summary>
    public string? LimitDetail { get; init; }

    /// <summary>The first line of the error it ended with, for the same-error breaker.</summary>
    public string? Error { get; init; }

    /// <summary>Whether it made a commit or wrote a record through the Theseus tools while it ran.</summary>
    public bool MadeProgress { get; init; }

    /// <summary>
    /// Whether it was the one resume, after a hard cost stop, that only writes the handoff. Such a run is counted in
    /// the budgets but not by the breakers: it was told to do nothing else.
    /// </summary>
    public bool IsHandoffRun { get; init; }

    /// <summary>Its process's exit code.</summary>
    public int ExitCode { get; init; }
}
