namespace Theseus.Core.Projects;

/// <summary>
/// Where a piece of work is, in one state and one sentence (1.1d AC 7): the first that applies of waiting on you,
/// stuck, busy, holdouts not run, all done, nothing done, and otherwise the next stage to do.
/// </summary>
/// <param name="State">The state its box and the project's counts show.</param>
/// <param name="Text">The sentence, such as "Waiting on you: accept the spec" or "Next: Review".</param>
public sealed record PieceStatus(StageNodeState State, string Text)
{
    /// <summary>The state's mark.</summary>
    public string Mark => StageNodeStates.Mark(State);

    /// <summary>Works out a piece's status from its stages.</summary>
    public static PieceStatus For(PieceOfWork piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        var stages = piece.Stages;

        if (stages.FirstOrDefault(stage => stage.State == StageNodeState.WaitingOnYou) is { } waiting)
        {
            return new PieceStatus(StageNodeState.WaitingOnYou, $"Waiting on you: {waiting.StatusReason}");
        }

        if (stages.FirstOrDefault(stage => stage.State == StageNodeState.Stuck) is { } stuck)
        {
            return new PieceStatus(StageNodeState.Stuck,
                stuck.IsSessionState ? $"Stuck: {stuck.StatusReason}" : $"Stuck at {stuck.Name}: {stuck.StatusReason}");
        }

        if (stages.FirstOrDefault(stage => stage.State == StageNodeState.Busy) is { } busy)
        {
            return new PieceStatus(StageNodeState.Busy, $"Busy: {piece.Workstream} working on {busy.Name}");
        }

        if (stages.FirstOrDefault(stage => stage.State == StageNodeState.HoldoutsNotRun) is { } holdouts)
        {
            return new PieceStatus(StageNodeState.HoldoutsNotRun, $"{holdouts.Name}: holdouts not run yet");
        }

        if (stages.All(stage => stage.State == StageNodeState.Done))
        {
            return new PieceStatus(StageNodeState.Done, "Done");
        }

        if (stages.All(stage => stage.State == StageNodeState.ToDo))
        {
            return piece.IsFreeform && piece.RecordsFolder is not null && piece.ObjectiveNumber is { } objective
                ? new PieceStatus(StageNodeState.ToDo, $"Freeform: done when objective {objective} is done")
                : new PieceStatus(StageNodeState.ToDo, "Not started: no records yet");
        }

        var next = StageOrder.NextToDo(stages, stage => stage.State);

        return next < 0
            ? new PieceStatus(StageNodeState.Done, "Done")
            : new PieceStatus(StageNodeState.ToDo, $"Next: {stages[next].Name}");
    }
}
