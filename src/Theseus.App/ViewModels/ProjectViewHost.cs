namespace Theseus.App.ViewModels;

/// <summary>
/// What showing a project view does to the board (1.1d AC 18, 32): the view draws no board cards (departure 3), so
/// while one is open nothing on the board is shown, selected or startable; and Esc clears the view's selection before
/// the board's, wherever keyboard focus is.
/// </summary>
public sealed class ProjectViewHost
{
    private readonly BoardViewModel _board;

    /// <summary>Creates the host over the board.</summary>
    public ProjectViewHost(BoardViewModel board)
    {
        ArgumentNullException.ThrowIfNull(board);

        _board = board;
    }

    /// <summary>A project was opened, or another one switched to: no board card stays shown or selected.</summary>
    public void Opened()
    {
        _board.SelectionOrder = () => [];
        _board.DeselectUnshown();
    }

    /// <summary>Back to the board: the grid's cards are shown again, and nothing from before stays selected.</summary>
    public void Closed()
    {
        _board.SelectionOrder = null;
        _board.DeselectUnshown();
    }

    /// <summary>Whether Esc has anything to clear: the open view's selection, or a selected card or project card.</summary>
    public bool CanClearSelection(ProjectViewModel? view) =>
        view?.Selection is not null || _board.Cards.Any(card => card.IsSelected) || _board.ProjectCards.Any(project => project.IsSelected);

    /// <summary>
    /// Esc: closes the open view's Properties overlay, leaving its selection (1.1d AC 53), or else clears its selection,
    /// when it has one, and says so (true); otherwise false, for the board's own clearing to run.
    /// </summary>
    public static bool ClearViewSelection(ProjectViewModel? view)
    {
        if (view?.Selection is null)
        {
            return false;
        }

        view.ClearSelectionCommand.Execute(null);

        return true;
    }
}
