using Theseus.Core.Sessions;

namespace Theseus.App.ViewModels;

/// <summary>One workflow a session ran, as the drawer shows it.</summary>
public sealed class WorkflowRunViewModel
{
    private readonly WorkflowRun _run;

    /// <summary>Creates the row.</summary>
    public WorkflowRunViewModel(WorkflowRun run) => _run = run;

    /// <summary>The name, size and outcome on one line.</summary>
    public string Headline => _run.Describe;

    /// <summary>The phases it declared, so what it set out to do is legible without the script.</summary>
    public string Phases => string.Join(" → ", _run.Phases
        .Select(phase => phase.Title)
        .Where(title => !string.IsNullOrWhiteSpace(title)));

    /// <summary>The workflow's own description, shown on hover.</summary>
    public string Summary => _run.Summary ?? string.Empty;

    /// <summary>When it ran.</summary>
    public DateTimeOffset? Timestamp => _run.Timestamp;

    /// <summary>How long it took, in words.</summary>
    public string Duration => _run.DurationMs <= 0
        ? string.Empty
        : TimeSpan.FromMilliseconds(_run.DurationMs) is { TotalHours: >= 1 } long_
            ? $"{long_.TotalHours:0.#} h"
            : $"{TimeSpan.FromMilliseconds(_run.DurationMs).TotalMinutes:0} min";
}
