namespace Theseus.Core.Projects;

/// <summary>The gate after one stage of a workstream.</summary>
/// <param name="Stage">The stage, as the view's column.</param>
/// <param name="State">Where the gate stands.</param>
/// <param name="IsOwnersGate">Whether only the owner passes it (the spec, or a bug's rule).</param>
/// <param name="Note">What the owner is told beside it, such as the evidence's result, or null.</param>
public sealed record StageGate(PipelineStage Stage, GateState State, bool IsOwnersGate, string? Note = null);
