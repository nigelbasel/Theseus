namespace Theseus.Core.Projects;

/// <summary>A project file read in the <c>method-v0.2</c> shape (method/project.md).</summary>
/// <param name="Name">The project home folder's name: the project's identity on this machine.</param>
/// <param name="Title">The file's title.</param>
/// <param name="Kit">The kit version its <c>Kit:</c> line names.</param>
/// <param name="PrimaryBranch">The project's primary branch, from its <c>Primary branch:</c> line.</param>
/// <param name="Phases">Its phases, in order.</param>
public sealed record ProjectFile(string Name, string Title, string Kit, string PrimaryBranch, IReadOnlyList<ProjectPhase> Phases)
{
    /// <summary>
    /// The slugs its <c>**Before method-v0.2:**</c> line names: work finished before the project moved to method-v0.2,
    /// shown at its stage with no gate markers (1.1 AC 9).
    /// </summary>
    public IReadOnlySet<string> BeforeKit { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Its <c>## Rounds</c> table's rows, in order; empty when it has none (1.1f B6).</summary>
    public IReadOnlyList<ProjectRound> RoundsTable { get; init; } = [];

    /// <summary>The date of its first Decisions row, or null when it has none (1.1f B8).</summary>
    public DateOnly? FirstDecision { get; init; }

    /// <summary>
    /// The earliest date written anywhere in the file, standing in for its first commit (1.1f B8): records are read as
    /// plain files, never with git (1.1b AC 2). Null when it has none.
    /// </summary>
    public DateOnly? EarliestDate { get; init; }

    /// <summary>
    /// Its rounds: the Rounds table's, or the one round "Round 1" a file without the table reads as, begun on its first
    /// Decisions row's date, else the earliest date the file names (1.1f B8, D4).
    /// </summary>
    public IReadOnlyList<ProjectRound> Rounds => RoundsTable.Count > 0 ? RoundsTable : [ProjectRound.Implicit(FirstDecision ?? EarliestDate)];

    /// <summary>The round with this number, or null.</summary>
    public ProjectRound? Round(int number) => Rounds.FirstOrDefault(round => round.Number == number);

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
