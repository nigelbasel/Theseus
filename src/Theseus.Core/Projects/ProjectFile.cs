namespace Theseus.Core.Projects;

/// <summary>A project file read in the <c>method-v0.2</c> shape (method/project.md).</summary>
/// <param name="Name">The file's name without <c>.md</c>: the project's identity on this machine.</param>
/// <param name="Title">The file's title.</param>
/// <param name="Kit">The kit version its <c>Kit:</c> line names.</param>
/// <param name="PrimaryBranch">The project's primary branch, from its <c>Primary branch:</c> line.</param>
/// <param name="Phases">Its phases, in order.</param>
public sealed record ProjectFile(string Name, string Title, string Kit, string PrimaryBranch, IReadOnlyList<ProjectPhase> Phases)
{
    /// <summary>Every objective, in file order.</summary>
    public IReadOnlyList<ProjectObjective> Objectives => [.. Phases.SelectMany(phase => phase.Objectives)];

    /// <summary>How many objectives are done (AC 11).</summary>
    public int ObjectivesDone => Objectives.Count(objective => objective.IsDone);

    /// <summary>The first phase with an objective not done, or null when every objective is done (AC 11).</summary>
    public ProjectPhase? CurrentPhase => Phases.FirstOrDefault(phase => phase.HasOpenObjective);

    /// <summary>Every workstream row, in file order.</summary>
    public IReadOnlyList<ProjectWorkstreamRow> Workstreams => [.. Objectives.SelectMany(objective => objective.Workstreams)];

    /// <summary>The objective whose table lists this slug, or null.</summary>
    public ProjectObjective? ObjectiveOf(string slug) =>
        Objectives.FirstOrDefault(objective =>
            objective.Workstreams.Any(row => string.Equals(row.Slug, slug, StringComparison.OrdinalIgnoreCase)));
}
