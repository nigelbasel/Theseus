namespace Theseus.Core.Sessions;

/// <summary>
/// One workflow a session ran, as recorded when the run finished. These files are written at the
/// end of a run and not during it, so this is a session's history and never its current state.
/// </summary>
/// <param name="RunId">The run's identifier.</param>
/// <param name="WorkflowName">The name the workflow gave itself.</param>
/// <param name="Summary">The workflow's one-line description.</param>
/// <param name="Status">How the run ended.</param>
/// <param name="AgentCount">How many agents it spawned in total.</param>
/// <param name="TotalTokens">Output tokens the whole run spent.</param>
/// <param name="TotalToolCalls">How many tool calls its agents made.</param>
/// <param name="DurationMs">How long it ran for.</param>
/// <param name="Timestamp">When the run was recorded.</param>
/// <param name="Phases">The phases it declared.</param>
public sealed record WorkflowRun(
    string? RunId,
    string? WorkflowName,
    string? Summary,
    string? Status,
    int AgentCount,
    long TotalTokens,
    int TotalToolCalls,
    long DurationMs,
    DateTimeOffset? Timestamp,
    IReadOnlyList<WorkflowPhase> Phases)
{
    /// <summary>Whether the run finished on its own terms.</summary>
    public bool Completed => string.Equals(Status, "completed", StringComparison.OrdinalIgnoreCase);

    /// <summary>The line a drawer shows: what it was, how big it got, and how it ended.</summary>
    public string Describe
    {
        get
        {
            var parts = new List<string> { WorkflowName ?? RunId ?? "workflow" };

            if (AgentCount > 0)
            {
                parts.Add($"{AgentCount} agent{(AgentCount == 1 ? string.Empty : "s")}");
            }

            if (TotalTokens > 0)
            {
                parts.Add($"{TokenCount.Format(TotalTokens)} tokens");
            }

            if (!string.IsNullOrWhiteSpace(Status))
            {
                parts.Add(Status);
            }

            return string.Join(" · ", parts);
        }
    }
}
