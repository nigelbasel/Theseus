namespace Theseus.Core.Projects;

/// <summary>A phase of the project file and its objectives, for the view grouped by objectives (1.1d AC 46).</summary>
/// <param name="Phase">The phase.</param>
/// <param name="Objectives">Every objective of the phase, in file order, each with its pieces of work.</param>
public sealed record PhaseGroup(ProjectPhase Phase, IReadOnlyList<ObjectiveGroup> Objectives)
{
    /// <summary>How many of its objectives are done.</summary>
    public int ObjectivesDone => Objectives.Count(group => group.Objective?.IsDone == true);

    /// <summary>Done when it has objectives and every one is done; a done phase starts folded.</summary>
    public bool IsDone => Objectives.Count > 0 && ObjectivesDone == Objectives.Count;
}
