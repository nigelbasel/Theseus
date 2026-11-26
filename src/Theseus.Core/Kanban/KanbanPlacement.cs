using Theseus.Core.Projects;

namespace Theseus.Core.Kanban;

/// <summary>
/// Which one column a piece of work sits in, its current stage (projects-kanban spec AC 9 and 10): the stage its session
/// last named while that stage is unfinished, else its first unfinished stage, else Done. A stage its pipeline lacks never
/// holds it, because only its own stages are looked at.
/// </summary>
public static class KanbanPlacement
{
    /// <summary>The column of a piece, from its stages, its session's last <c>now_at</c> and its status.</summary>
    public static KanbanColumn ColumnOf(PieceOfWork piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        return ColumnOf([.. piece.Stages.Select(stage => (stage.Stage, stage.State))], piece.NowAt?.Stage, piece.NowAt is not null,
            piece.Status.State);
    }

    /// <summary>The rule itself, over each stage's pipeline stage and state.</summary>
    /// <param name="stages">The piece's own stages, in pipeline order.</param>
    /// <param name="nowAt">The stage its session last named; null for freeform's Work or when it named none.</param>
    /// <param name="saidNowAt">Whether its session named a stage at all.</param>
    /// <param name="status">The piece's status state.</param>
    public static KanbanColumn ColumnOf(IReadOnlyList<(PipelineStage? Stage, StageNodeState State)> stages, PipelineStage? nowAt,
        bool saidNowAt, StageNodeState status)
    {
        ArgumentNullException.ThrowIfNull(stages);

        if (status is StageNodeState.Done or StageNodeState.Answered)
        {
            return KanbanColumn.Done;
        }

        var named = saidNowAt ? IndexOf(stages, stage => stage.Stage == nowAt) : -1;

        if (named >= 0 && !StageNodeStates.IsFinished(stages[named].State))
        {
            return KanbanColumns.Of(stages[named].Stage);
        }

        var first = IndexOf(stages, stage => !StageNodeStates.IsFinished(stage.State));

        return first >= 0 ? KanbanColumns.Of(stages[first].Stage) : KanbanColumn.Done;
    }

    private static int IndexOf(IReadOnlyList<(PipelineStage? Stage, StageNodeState State)> stages,
        Func<(PipelineStage? Stage, StageNodeState State), bool> matches)
    {
        for (var index = 0; index < stages.Count; index++)
        {
            if (matches(stages[index]))
            {
                return index;
            }
        }

        return -1;
    }
}
