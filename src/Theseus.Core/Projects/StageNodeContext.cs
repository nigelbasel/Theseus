namespace Theseus.Core.Projects;

/// <summary>What a stage's actions depend on beyond the stage itself: the piece's records and its card's session.</summary>
/// <param name="Workstream">The name of the card that works the piece.</param>
/// <param name="RecordsFolder">The piece's <c>work\&lt;piece&gt;\</c> folder, or null when there is none.</param>
/// <param name="HasCard">Whether its card is on the board.</param>
/// <param name="IsCurrentPiece">Whether the piece is its card's current piece (its last row, code fact 3).</param>
/// <param name="HasSession">Whether its card has a session open now.</param>
/// <param name="CurrentPiece">The card's current piece, in whichever project owns it, or null when it has none.</param>
/// <param name="CanStart">Whether its card could be started now (<see cref="PieceCard.CanStart"/>).</param>
public sealed record StageNodeContext(
    string Workstream,
    string? RecordsFolder,
    bool HasCard,
    bool IsCurrentPiece,
    bool HasSession,
    string? CurrentPiece,
    bool CanStart);
