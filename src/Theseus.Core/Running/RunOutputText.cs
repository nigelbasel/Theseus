using Theseus.Core.Budgets;
using Theseus.Core.Sessions;

namespace Theseus.Core.Running;

/// <summary>A headless run's output as the owner reads it in its Output window: one line per message, tool call and turn.</summary>
public static class RunOutputText
{
    /// <summary>The readable line for one line of the run's log, or null for one not shown.</summary>
    public static string? Render(string line) => RunStreamParser.Parse(line) switch
    {
        RunInitEvent init => $"Session began: {init.SessionId}",
        RunAssistantEvent { Line: { } text } => text,
        RunResultEvent result => $"Turn ended: {result.Subtype}"
                                 + (result.TotalCostUsd is { } cost ? $" · {Money.Usd(cost)} so far" : string.Empty)
                                 + $" · {TokenCount.Format(result.Usage.Counted)} tokens"
                                 + (result.IsError && result.Text is { } error ? $" · {error}" : string.Empty),
        _ => null,
    };
}
