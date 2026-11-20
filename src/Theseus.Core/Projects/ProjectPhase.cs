namespace Theseus.Core.Projects;

/// <summary>A <c>## Phase n: name</c> section and its objectives.</summary>
/// <param name="Number">Its number, from 0.</param>
/// <param name="Name">Its name.</param>
/// <param name="ExitGate">Its exit gate's text; several exit gate paragraphs are joined in order. Empty when it has none.</param>
/// <param name="Objectives">Its objectives, in order.</param>
/// <param name="Line">The heading's line, from 1.</param>
public sealed record ProjectPhase(int Number, string Name, string ExitGate, IReadOnlyList<ProjectObjective> Objectives, int Line)
{
    /// <summary>Whether it has an objective not done.</summary>
    public bool HasOpenObjective => Objectives.Any(objective => !objective.IsDone);
}
