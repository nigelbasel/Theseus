using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>One count chip of the project's summary, such as "◆ 3 waiting on you" (1.1d AC 19).</summary>
/// <param name="State">The state it counts, which colours it.</param>
/// <param name="Count">How many pieces of work have that status.</param>
/// <param name="Words">What they are, such as "waiting on you".</param>
public sealed record StateCountViewModel(StageNodeState State, int Count, string Words)
{
    /// <summary>Shows only pieces in this state, Ctrl adding it (1.1e item 26); null on the project card.</summary>
    public System.Windows.Input.ICommand? Command { get; init; }

    /// <summary>Whether the view is filtered to this state.</summary>
    public bool IsActive { get; init; }

    /// <summary>Its hover: what a click does.</summary>
    public string ToolTip => $"Show only what is {StageNodeStates.Word(State).ToLowerInvariant()}; Ctrl+click adds it; click again or Esc to show everything";

    private static readonly (StageNodeState State, string Words)[] Order =
    [
        (StageNodeState.Done, "done"), (StageNodeState.Busy, "busy"), (StageNodeState.WaitingOnYou, "waiting on you"),
        (StageNodeState.HoldoutsNotRun, "holdouts not run"), (StageNodeState.Stuck, "stuck"), (StageNodeState.ToDo, "to do"),
    ];

    // The states 1.1f added (E26 to E29) are counted only when there are some, so the six chips stay as AC 19 has them.
    private static readonly (StageNodeState State, string Words)[] Added =
    [
        (StageNodeState.Answered, "answered"), (StageNodeState.HandedOn, "handed on"),
        (StageNodeState.RecordsElsewhere, "records elsewhere"), (StageNodeState.NotInProject, "not in project.md"),
    ];

    /// <summary>The chip's text: mark, number and words.</summary>
    public string Text => $"{StageNodeStates.Mark(State)} {Count} {Words}";

    /// <summary>
    /// A summary's chips in AC 19's order, from <see cref="ProjectSummary"/> only (AC 10): all six in the project view,
    /// the ones that are not zero on the project card (AC 39).
    /// </summary>
    public static IReadOnlyList<StateCountViewModel> For(ProjectSummary summary, bool zeros)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return
        [
            .. Order.Select(entry => new StateCountViewModel(entry.State, summary.CountOf(entry.State), entry.Words))
                .Where(chip => zeros || chip.Count > 0),
            .. Added.Select(entry => new StateCountViewModel(entry.State, summary.CountOf(entry.State), entry.Words))
                .Where(chip => chip.Count > 0),
        ];
    }
}
