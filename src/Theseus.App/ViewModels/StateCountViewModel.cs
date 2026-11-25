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
        (StageNodeState.HoldoutsNotRun, "not yet checked"), (StageNodeState.Stuck, "stuck"), (StageNodeState.ToDo, "to do"),
    ];

    // The states 1.1f added (E26 to E29) are counted only when there are some, so the six chips stay as AC 19 has them.
    private static readonly (StageNodeState State, string Words)[] Added =
    [
        (StageNodeState.Answered, "answered"), (StageNodeState.HandedOn, "handed on"),
        (StageNodeState.RecordsElsewhere, "records elsewhere"), (StageNodeState.NotInProject, "not in project.md"),
    ];

    /// <summary>The chip's text: mark, number and words.</summary>
    public string Text => $"{StageNodeStates.Mark(State)} {Count} {Words}";

    /// <summary>Every state in the chips' order: AC 19's six, then those 1.1f added.</summary>
    public static IReadOnlyList<StageNodeState> States { get; } = [.. Order.Concat(Added).Select(entry => entry.State)];

    /// <summary>What a chip for this many in this state says, such as "◐ 4 not yet checked".</summary>
    public static string TextOf(StageNodeState state, int count) =>
        new StateCountViewModel(state, count, Order.Concat(Added).Single(entry => entry.State == state).Words).Text;

    /// <summary>
    /// A summary's chips in AC 19's order, from <see cref="ProjectSummary"/> (AC 10): all six in the project view, the ones
    /// that are not zero on the project card (AC 39).
    /// </summary>
    /// <param name="summary">The pieces' counts.</param>
    /// <param name="zeros">Whether a state with none keeps its chip.</param>
    /// <param name="waiting">
    /// How many items of Waits for you there are, which "waiting on you" counts in place of the pieces in that state: a
    /// report or a proposal waits on the owner from a box in another state, and the chip, the heading and the sentence
    /// must agree (git-22, 2026-11-24). Null counts the pieces.
    /// </param>
    public static IReadOnlyList<StateCountViewModel> For(ProjectSummary summary, bool zeros, int? waiting = null)
    {
        ArgumentNullException.ThrowIfNull(summary);

        int CountOf(StageNodeState state) => state == StageNodeState.WaitingOnYou && waiting is { } items ? items : summary.CountOf(state);

        return
        [
            .. Order.Select(entry => new StateCountViewModel(entry.State, CountOf(entry.State), entry.Words))
                .Where(chip => zeros || chip.Count > 0),
            .. Added.Select(entry => new StateCountViewModel(entry.State, summary.CountOf(entry.State), entry.Words))
                .Where(chip => chip.Count > 0),
        ];
    }
}
