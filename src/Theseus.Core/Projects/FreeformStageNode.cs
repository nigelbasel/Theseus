namespace Theseus.Core.Projects;

/// <summary>
/// A freeform piece's one stage, Work (code fact 1): <see cref="StageCalculator"/> gives freeform work no stages and no
/// gates, so it is done only when it was listed before method-v0.2 or its objective is done.
/// </summary>
public sealed class FreeformStageNode : WorkStageNode
{
    /// <summary>The name of a freeform piece's one stage.</summary>
    public const string WorkName = "Work";

    internal FreeformStageNode(StageNodeState state, string? reason, bool isSessionState, StageNodeContext context)
        : base(null, WorkName, state, reason, null, false, isSessionState, context)
    {
    }
}
