namespace Theseus.Core.Projects;

/// <summary>An objective of the project file and its pieces of work, for the view grouped by objectives (1.1d AC 46, 47).</summary>
/// <param name="Objective">The objective, or null for the pieces no project file lists (1.1d AC 13).</param>
/// <param name="Pieces">Its pieces of work, in file order; none while nothing works on it yet.</param>
public sealed record ObjectiveGroup(ProjectObjective? Objective, IReadOnlyList<PieceOfWork> Pieces);
