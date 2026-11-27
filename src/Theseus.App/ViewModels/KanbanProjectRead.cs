using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>What a refresh read of one project, kept for the kanban; pieces and overview are null when its file does not read.</summary>
/// <param name="Reading">Its reading.</param>
/// <param name="Pieces">Its pieces of work.</param>
/// <param name="Overview">Its overview.</param>
/// <param name="Lead">Its lead card's key.</param>
public sealed record KanbanProjectRead(ProjectFileReading Reading, IReadOnlyList<PieceOfWork>? Pieces, ProjectOverview? Overview, string? Lead);
