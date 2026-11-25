namespace Theseus.Core.Projects;

/// <summary>
/// A piece's records as Theseus shows them: one row per stage with its document's purpose and state, then the folder's
/// other files, so the owner reads Theseus's screen first and the raw folder is one click further (Nigel, 2026-11-22).
/// </summary>
public static class PieceRecords
{
    /// <summary>The rows of a piece's Records view: its stages in order, then "Other records".</summary>
    public static IReadOnlyList<RecordEntry> For(PieceOfWork piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        List<RecordEntry> stages = piece.IsFreeform || piece.Stages is [{ State: StageNodeState.RecordsElsewhere }]
            ? []
            : piece.Stages.Select(stage => new RecordEntry(stage.Name, Purpose(piece.Pipeline, stage.Stage), stage.State, stage.RecordFile)
            {
                StateText = StateText(piece, stage),
            }).ToList();

        if (piece.RecordsFolder is not { } folder || !Directory.Exists(folder))
        {
            return stages;
        }

        var stageFiles = piece.Stages.Select(stage => stage.RecordName).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var others = Directory.EnumerateFiles(folder)
            .Where(file => !stageFiles.Contains(Path.GetFileName(file)))
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(file => new RecordEntry(Path.GetFileName(file), "Other records", StageNodeState.Done, file)
            {
                StateText = "written",
                IsOther = true,
            });

        return [.. stages, .. others];
    }

    /// <summary>What the view says when there is nothing to list, or null when there is.</summary>
    public static string? NoneText(PieceOfWork piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        if (piece.Stages is [{ State: StageNodeState.RecordsElsewhere }])
        {
            return "Records are kept outside the project, so Theseus has none to show.";
        }

        if (piece.RecordsFolder is null)
        {
            return "Nothing is recorded in the project for this piece of work yet.";
        }

        return For(piece).Any(entry => entry.HasFile) ? null : "Its records folder holds no documents yet.";
    }

    private static string StateText(PieceOfWork piece, StageNode stage) => stage.State switch
    {
        StageNodeState.Done when stage is SpecStageNode && piece.Acceptance is { } accepted =>
            $"accepted by you on {accepted.AcceptedUtc.ToLocalTime():d MMM}",
        StageNodeState.NotNeeded => $"skipped: {stage.Reason}",
        StageNodeState.DoneNoRecord => "done with no record",
        StageNodeState.HandedOn => $"handed on: {stage.Reason}",
        _ when stage.RecordFile is not null => "written",
        _ => "not written",
    };

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
