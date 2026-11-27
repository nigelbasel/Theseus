using Theseus.Core.Projects;

namespace Theseus.Core.Kanban;

/// <summary>The kanban's columns in order, with the words their headers use (projects-kanban spec AC 8).</summary>
public static class KanbanColumns
{
    /// <summary>Every column, left to right.</summary>
    public static IReadOnlyList<KanbanColumn> All { get; } = Enum.GetValues<KanbanColumn>();

    /// <summary>The header's name, with the other pipelines' names for the same column.</summary>
    public static string Header(KanbanColumn column) => column switch
    {
        KanbanColumn.Research => "Research / Reproduce",
        KanbanColumn.Spec => "Spec / Rule",
        KanbanColumn.Build => "Build (Implement, Fix, Work)",
        _ => Name(column),
    };

    /// <summary>The short name, for a collapsed column and a card's automation name.</summary>
    public static string Name(KanbanColumn column) => column switch
    {
        KanbanColumn.Lessons => "Lessons",
        _ => column.ToString(),
    };

    /// <summary>The column a pipeline stage sits in; freeform's Work, which has no pipeline stage, is Build.</summary>
    public static KanbanColumn Of(PipelineStage? stage) => stage switch
    {
        PipelineStage.Research => KanbanColumn.Research,
        PipelineStage.Spec => KanbanColumn.Spec,
        PipelineStage.Plan => KanbanColumn.Plan,
        PipelineStage.Implement or null => KanbanColumn.Build,
        PipelineStage.Verify => KanbanColumn.Verify,
        PipelineStage.Review => KanbanColumn.Review,
        PipelineStage.Compound => KanbanColumn.Lessons,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
    };
}
