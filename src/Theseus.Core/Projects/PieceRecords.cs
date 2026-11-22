namespace Theseus.Core.Projects;

/// <summary>
/// A piece's records as Theseus shows them: one row per stage with its document's purpose and state, so the owner reads
/// Theseus's screen first and the raw folder is one click further (brief, Records view and its principle, 2026-11-22).
/// </summary>
public static class PieceRecords
{
    /// <summary>The rows of a piece's Records view, in stage order; for freeform work, each document in its folder.</summary>
    public static IReadOnlyList<RecordEntry> For(PieceOfWork piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        if (piece.IsFreeform)
        {
            var state = piece.Stages.FirstOrDefault()?.State ?? StageNodeState.ToDo;

            return piece.RecordsFolder is { } folder && Directory.Exists(folder)
                ? [.. Directory.EnumerateFiles(folder, "*.md").Order(StringComparer.OrdinalIgnoreCase)
                    .Select(file => new RecordEntry(Path.GetFileNameWithoutExtension(file), "A record of the work", state, file))]
                : [];
        }

        return [.. piece.Stages.Select(stage => new RecordEntry(stage.Name, Purpose(piece.Pipeline, stage.Stage), stage.State, stage.RecordFile))];
    }

    /// <summary>What the view says when there is nothing to list, or null when there is.</summary>
    public static string? NoneText(PieceOfWork piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        if (piece.Stages is [{ State: StageNodeState.RecordsElsewhere }])
        {
            return "Its records are kept outside the project, so Theseus has none to show.";
        }

        if (piece.RecordsFolder is null)
        {
            return "Nothing is recorded in the project for this piece of work yet.";
        }

        return For(piece).Any(entry => entry.HasFile) ? null : "Its records folder holds no documents yet.";
    }

    private static string Purpose(PipelineKind? pipeline, PipelineStage? stage) => (pipeline, stage) switch
    {
        (PipelineKind.Bug, PipelineStage.Research) => "How the fault was reproduced",
        (PipelineKind.Bug, PipelineStage.Spec) => "The rule the fix must keep, for you to accept",
        (PipelineKind.Bug, PipelineStage.Implement) => "What caused the fault and what was changed",
        (_, PipelineStage.Research) => "What was found out before deciding what to build",
        (_, PipelineStage.Spec) => "What will be built, for you to accept",
        (_, PipelineStage.Plan) => "How it will be built",
        (_, PipelineStage.Implement) => "What was built, and how",
        (_, PipelineStage.Verify) => "The evidence that it works, and the independent check",
        (_, PipelineStage.Review) => "A review by a session that did not build it",
        (_, PipelineStage.Compound) => "Lessons for the method",
        _ => "A record of the work",
    };
}
