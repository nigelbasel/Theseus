using Theseus.Core.Domain;

namespace Theseus.App.ViewModels;

/// <summary>
/// Takes the paused mark off a card once it has really started from a project's Start (1.1d AC 43; review of PR 3, item
/// 3). The confirmation hands the start a workstream with its mark off; the card keeps its mark until the launch has
/// happened, so a cancelled confirmation, or a start Theseus refuses, changes nothing on the board.
/// </summary>
public static class StartedMarks
{
    /// <summary>After a launch: clears and saves the card's paused mark when the workstream started had it off.</summary>
    public static void Clear(BoardViewModel board, Workstream launched)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(launched);

        if (launched.IsPaused || board.Card(launched.Key) is not { IsPaused: true } card)
        {
            return;
        }

        card.Workstream = card.Workstream with { IsPaused = false };
        board.Persist(card);
    }
}
