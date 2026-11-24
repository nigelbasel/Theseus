namespace Theseus.Core.Projects;

/// <summary>A piece of work in the project's tree, with the pieces it hands work to under it (1.1d AC 11).</summary>
/// <param name="Piece">The piece.</param>
/// <param name="Children">The pieces its card hands work to, in file order.</param>
/// <param name="ReportBack">Its report-back mark, or null when its card reports back to no piece here (AC 14).</param>
public sealed record PieceNode(PieceOfWork Piece, IReadOnlyList<PieceNode> Children, ReportBackMark? ReportBack);
