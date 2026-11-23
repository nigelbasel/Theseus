namespace Theseus.Core.Projects;

/// <summary>
/// Verify. While its holdouts are not run it offers Independent check, which acts on a card and checks its current
/// piece (code fact 4): on an older piece it is offered disabled, saying why. It names the verifier cards checking it,
/// since verifiers are never pieces of their own (AC 13).
/// </summary>
public sealed class VerifyStageNode : StageNode
{
    internal VerifyStageNode(StageNodeState state, string? reason, string recordName, bool isWritten, bool isSessionState,
        StageNodeContext context, IReadOnlyList<string> verifiers)
        : base(PipelineStage.Verify, "Verify", state, reason, recordName, isWritten, isSessionState, context)
    {
        Verifiers = verifiers;
    }

    /// <summary>The names of the verifier cards that report back to this piece's card, in link order.</summary>
    public IReadOnlyList<string> Verifiers { get; }

    /// <inheritdoc />
    protected override IEnumerable<StageActionOffer> OwnActions()
    {
        if (State != StageNodeState.HoldoutsNotRun)
        {
            yield break;
        }

        const string label = "Independent check…";

        if (Context.IsCurrentPiece)
        {
            yield return new StageActionOffer(StageAction.VerifyWithHoldouts, label);
        }
        else if (Context.CurrentPiece is { } current)
        {
            yield return new StageActionOffer(StageAction.VerifyWithHoldouts, label, false,
                $"{Context.Workstream} has moved on to {current}; Independent check checks a card's current work");
        }
        else
        {
            yield return new StageActionOffer(StageAction.VerifyWithHoldouts, label, false,
                $"{Context.Workstream} is not on the board; Independent check checks a card's current work");
        }
    }
}
