namespace Theseus.Core.Projects;

/// <summary>A stage that only writes a record: Research (a bug's Reproduce), Plan, Review and Compound.</summary>
public sealed class RecordStageNode : StageNode
{
    internal RecordStageNode(PipelineStage stage, string name, StageNodeState state, string? reason, string? recordName,
        bool isWritten, bool isSessionState, StageNodeContext context)
        : base(stage, name, state, reason, recordName, isWritten, isSessionState, context)
    {
    }

    /// <inheritdoc />
    protected override IEnumerable<StageActionOffer> OwnActions() => [];
}
