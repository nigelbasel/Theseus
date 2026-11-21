using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

/// <summary>
/// Where a project card sits on an axis the board works out rather than the owner places (1.1e AC 4): by its cards.
/// </summary>
public static class ProjectBoardStanding
{
    /// <summary>
    /// The session-state column a project sits in: that of its most urgent running card (Needs you, then Working, then
    /// Idle), Not running when none runs, and Done when every piece of work is done (decision 1 to confirm, 2026-11-21).
    /// </summary>
    public static string StateColumn(IReadOnlyList<PieceOfWork> pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);

        var running = pieces
            .Where(piece => piece.IsCurrentPiece && piece.Card is { HasSession: true })
            .Select(piece => piece.Card!.Lifecycle)
            .ToList();

        if (running.Contains(SessionLifecycle.NeedsAttention))
        {
            return BoardView.NeedsYou;
        }

        if (running.Any(lifecycle => lifecycle is SessionLifecycle.Launching or SessionLifecycle.Running))
        {
            return BoardView.Working;
        }

        if (running.Count > 0)
        {
            return BoardView.Idle;
        }

        return pieces.Count > 0 && pieces.All(piece => piece.Status.State == StageNodeState.Done)
            ? BoardView.Done
            : BoardView.NotRunning;
    }

    /// <summary>The key of the first card its pieces name, in the file's order, whose folder or chain row it sits in; null with none.</summary>
    public static string? FirstCardKey(IReadOnlyList<PieceOfWork> pieces)
    {
        ArgumentNullException.ThrowIfNull(pieces);

        return pieces.FirstOrDefault(piece => piece.Card is not null)?.CardKey;
    }
}
