namespace Theseus.Core.Projects;

/// <summary>A workstream moving on from one piece of work to its next row in the project file (1.1d AC 12).</summary>
/// <param name="From">The earlier piece.</param>
/// <param name="To">The next row of the same workstream.</param>
public sealed record ThenWorkedOnLink(PieceOfWork From, PieceOfWork To);
