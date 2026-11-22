using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// What a project card says and can do, from the same pieces of work the project view is built from (1.1d AC 39, 43):
/// the view's sentence and its counts with the zeros left out, whether any card has a session, and what its Start and
/// Stop would do. The shell calls it on every refresh, so it is the one place this is worked out.
/// </summary>
public static class ProjectCardFace
{
    /// <summary>
    /// Fills a readable project's card. True when what its ▶ or ■ can do has changed, so the caller has their enabled
    /// state looked at again: nothing else tells WPF a command's answer moved with a session.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <param name="pieces">Its project's pieces of work.</param>
    /// <param name="runState">Its run state, when the project is stopped (1.1e AC 8); null when it is not.</param>
    /// <param name="stoppedByOwner">The cards the owner stopped by hand (1.1e AC 10).</param>
    public static bool Fill(ProjectCardViewModel card, IReadOnlyList<PieceOfWork> pieces, IReadOnlyList<string>? runState = null,
        IReadOnlyCollection<string>? stoppedByOwner = null, ProjectOverview? overview = null)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(pieces);

        var summary = ProjectSummary.For(pieces);
        var could = (card.StartPlan?.CanStart, card.StopPlan?.CanStop);

        // The card and the view say the same sentence, from the one overview (1.1f G33).
        card.Sentence = overview?.Sentence ?? summary.Sentence;
        card.Counts = StateCountViewModel.For(summary, zeros: false);
        card.IsLive = pieces.Any(piece => piece.IsCurrentPiece && piece.Card is { HasSession: true });
        card.StateColumn = ProjectBoardStanding.StateColumn(pieces);
        card.FirstCardKey = ProjectBoardStanding.FirstCardKey(pieces);
        card.RunState = runState;
        card.MemberKeys = [.. pieces.Select(piece => piece.CardKey).Distinct(StringComparer.OrdinalIgnoreCase)];
        card.StartPlan = ProjectStart.For(card.Title, pieces, runState, stoppedByOwner);
        card.StopPlan = ProjectStop.For(card.Title, pieces);

        return could != (card.StartPlan.CanStart, card.StopPlan.CanStop);
    }

    /// <summary>Empties a dimmed card of what only a readable project has: it keeps its reason, mark and colour (AC 44).</summary>
    public static void Dim(ProjectCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        card.Sentence = string.Empty;
        card.Counts = [];
        card.IsLive = false;
        card.RunState = null;
        card.MemberKeys = [];
        card.StateColumn = Theseus.Core.Domain.BoardView.NotRunning;
        card.FirstCardKey = null;
        card.StartPlan = null;
        card.StopPlan = null;
    }
}
