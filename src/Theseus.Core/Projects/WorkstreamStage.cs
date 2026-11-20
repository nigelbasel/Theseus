namespace Theseus.Core.Projects;

/// <summary>Where a slug's work stands: the stages reached, the gate after each, and the column its card sits in.</summary>
/// <param name="Pipeline">The pipeline it runs, or null when its objective has none chosen.</param>
/// <param name="Reached">The stages whose artefact exists, in order.</param>
/// <param name="Gates">The gate after each of its pipeline's stages; none for work from before method-v0.2 (AC 9).</param>
/// <param name="Position">The column its card sits in (AC 8), or null when it has none.</param>
/// <param name="Note">What is shown on the card: ahead of a gate, before method-v0.2; or null.</param>
/// <param name="Source">Where its work was read.</param>
public sealed record WorkstreamStage(
    PipelineKind? Pipeline,
    IReadOnlyList<PipelineStage> Reached,
    IReadOnlyList<StageGate> Gates,
    PipelineStage? Position,
    string? Note,
    WorkstreamSource Source)
{
    /// <summary>The evidence block's gates claims, shown for information only (AC 7).</summary>
    public IReadOnlyList<EvidenceGateClaim> EvidenceClaims { get; init; } = [];

    /// <summary>Whether anything of it has been reached.</summary>
    public bool HasStarted => Reached.Count > 0;

    /// <summary>The gate after a stage, or null when its pipeline has no such stage or the work shows no gates.</summary>
    public StageGate? GateAfter(PipelineStage stage) => Gates.FirstOrDefault(gate => gate.Stage == stage);

    /// <summary>Whether the owner's gate is reached and waiting on him (AC 15 acts on it).</summary>
    public bool IsWaitingOnOwner => Gates.Any(gate =>
        gate.IsOwnersGate && gate.State is GateState.Waiting or GateState.ChangedSinceAccepted);
}
