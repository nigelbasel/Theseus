namespace Theseus.Core.Projects;

/// <summary>
/// The stage a session works in: a feature's Implement, or a bug's Fix. Focus, Start and Stop act on a card, so they
/// are offered only on its card's current piece (code facts 3 and 5): Focus and Stop with a session, Start without one
/// and only where Start would start it.
/// </summary>
public class WorkStageNode : StageNode
{
    internal WorkStageNode(PipelineStage? stage, string name, StageNodeState state, string? reason, string? recordName,
        bool isWritten, bool isSessionState, StageNodeContext context)
        : base(stage, name, state, reason, recordName, isWritten, isSessionState, context)
    {
    }

    /// <inheritdoc />
    protected override IEnumerable<StageActionOffer> OwnActions()
    {
        if (!Context.IsCurrentPiece)
        {
            yield break;
        }

        if (Context.HasSession)
        {
            yield return new StageActionOffer(StageAction.Focus, $"⧉ Focus {Context.Workstream}");
            yield return new StageActionOffer(StageAction.Stop, "■ Stop");
        }
        else if (Context.CanStart)
        {
            yield return new StageActionOffer(StageAction.Start, "▶ Start");
        }
    }
}
