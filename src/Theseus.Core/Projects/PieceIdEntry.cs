namespace Theseus.Core.Projects;

/// <summary>One piece of work's stable id, as <c>piece-ids.json</c> keeps it (7.3).</summary>
/// <param name="Id">The id, given once and never changed.</param>
/// <param name="Project">The project the piece is in.</param>
/// <param name="Slug">Its slug now; a re-slug changes this, never the id.</param>
/// <param name="Cards">Every card that has worked under it, by key: its own, verifiers, hand-overs.</param>
public sealed record PieceIdEntry(string Id, string Project, string Slug, IReadOnlyList<string> Cards);
