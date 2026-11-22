namespace Theseus.Core.Projects;

/// <summary>
/// One cell of the phase strip: "Phase n: name" and one line, "✓ gate passed", "● under way · a of b objectives met",
/// "not started" or "not planned" (1.1f G34).
/// </summary>
/// <param name="Phase">The phase.</param>
/// <param name="Line">Its one line.</param>
/// <param name="State">The state its line is coloured as.</param>
public sealed record PhaseCell(ProjectPhase Phase, string Line, StageNodeState State)
{
    /// <summary>"Phase 1: Evidence".</summary>
    public string Title => $"Phase {Phase.Number}: {Phase.Name}";
}
