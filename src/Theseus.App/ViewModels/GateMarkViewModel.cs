using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// The decision diamond on a piece's stage strip between Spec and Plan (Rule and Fix for a bug): ◇ not passed, amber when
/// it waits for you, ◆ passed (1.1f D21). Freeform pieces have none.
/// </summary>
/// <param name="IsPassed">Whether the owner accepted the spec as it reads now.</param>
/// <param name="IsWaiting">Whether it waits for the owner to accept it.</param>
/// <param name="ToolTip">Its hover: passed when, waiting for you, or not reached yet.</param>
public sealed record GateMarkViewModel(bool IsPassed, bool IsWaiting, string ToolTip)
{
    /// <summary>◆ passed, ◇ otherwise.</summary>
    public string Mark => IsPassed ? "◆" : "◇";

    /// <summary>What its state is called, for UI Automation.</summary>
    public string AutomationName => IsPassed ? "Gate passed" : IsWaiting ? "Gate waiting for you" : "Gate not reached";

    /// <summary>The gate of a piece, or null when it has none (freeform, or no spec stage).</summary>
    public static GateMarkViewModel? For(PieceOfWork piece)
    {
        ArgumentNullException.ThrowIfNull(piece);

        if (piece.IsFreeform || piece.Stages.OfType<SpecStageNode>().FirstOrDefault() is not { } spec)
        {
            return null;
        }

        var noun = piece.Pipeline == PipelineKind.Bug ? "rule" : "spec";

        if (spec.AwaitsAcceptance)
        {
            return new GateMarkViewModel(false, true, $"Waiting for you to accept the {noun}");
        }

        if (spec.State == StageNodeState.Done && piece.Acceptance is { } accepted)
        {
            return new GateMarkViewModel(true, false, $"Passed: you accepted the {noun} {accepted.AcceptedUtc.ToLocalTime():d MMM}");
        }

        if (spec.State == StageNodeState.RecordsElsewhere)
        {
            return new GateMarkViewModel(false, false, $"Not passed here: {spec.Reason}");
        }

        return spec.State is StageNodeState.Done or StageNodeState.DoneNoRecord
            ? new GateMarkViewModel(true, false, $"Passed: the {noun} was done before the gates were kept")
            : new GateMarkViewModel(false, false, "Not reached yet");
    }
}
