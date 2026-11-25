namespace Theseus.Core.Projects;

/// <summary>A stage a session said it skipped, through <c>now_at(stage, "skipped: reason")</c> (1.1f E30a).</summary>
/// <param name="Project">The project.</param>
/// <param name="Piece">The piece of work.</param>
/// <param name="Stage">The stage skipped.</param>
/// <param name="Reason">Why, in the session's words.</param>
/// <param name="At">When it said so.</param>
public sealed record StageSkip(string Project, string Piece, PipelineStage Stage, string Reason, DateTimeOffset At);
