namespace Theseus.Core.Projects;

/// <summary>
/// What a session last said it is doing, through <c>now_at</c> (1.1f E24, D8): the only source of a busy stage.
/// </summary>
/// <param name="Stage">The stage it named, or null for a freeform piece's one stage.</param>
/// <param name="Doing">What it is doing, in under 80 characters.</param>
/// <param name="At">When it said so.</param>
public sealed record SessionStageReport(PipelineStage? Stage, string Doing, DateTimeOffset At);
