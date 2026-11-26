namespace Theseus.Core.Projects;

/// <summary>The owner's gate: a feature's Spec, or a bug's Rule. It offers the spec to read and accept while it waits on him.</summary>
public sealed class SpecStageNode : StageNode
{
    internal SpecStageNode(PipelineStage stage, string name, StageNodeState state, string? reason, string? recordName,
        bool isWritten, bool isSessionState, StageNodeContext context, bool awaitsAcceptance, bool isChangedSinceAccepted)
        : base(stage, name, state, reason, recordName, isWritten, isSessionState, context)
    {
        AwaitsAcceptance = awaitsAcceptance;
        IsChangedSinceAccepted = isChangedSinceAccepted;
    }

    /// <summary>Whether it is written and waits for the owner to accept it as it now reads.</summary>
    public bool AwaitsAcceptance { get; }

    /// <summary>Whether he accepted it once and it has changed since.</summary>
    public bool IsChangedSinceAccepted { get; }

    /// <summary>"accept the spec" (a bug's "accept the rule"), with "again" once it has changed since he accepted it.</summary>
    public override string? StatusReason => AwaitsAcceptance
        ? $"accept the {Name.ToLowerInvariant()}{(IsChangedSinceAccepted ? " again" : string.Empty)}"
        : Reason;

    /// <inheritdoc />
    protected override IEnumerable<StageActionOffer> OwnActions()
    {
        if (AwaitsAcceptance)
        {
            yield return new StageActionOffer(StageAction.OpenSpec, "View spec");
            yield return new StageActionOffer(StageAction.AcceptSpec, "Accept spec…");
        }
    }
}
