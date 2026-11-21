using System.Windows;
using Theseus.App.ViewModels;

namespace Theseus.App.Services;

/// <summary>
/// A press on the board and what moving it does (1.1d AC 42; review of PR 3, items 5 and 6). The card or project card
/// pressed is taken at the press: the move is never asked what is under the pointer, which used to drag nothing once the
/// pointer had left a project card. No drag starts where nothing can be dropped (columns by session state), so a click
/// that wobbles still selects; a move long enough to be a real drag there is told why, once.
/// </summary>
public sealed class BoardPress
{
    // A refusal waits for a move this many times the system's drag distance, so a wobbling click says nothing.
    private const double RefusalDistance = 4;

    private Point? _origin;
    private IBoardCard? _item;
    private bool _refused;

    /// <summary>The left button went down here, on this element.</summary>
    public void Down(DependencyObject? source, Point at)
    {
        _origin = at;
        _item = BoardGesture.ItemAt(source);
        _refused = false;
    }

    /// <summary>The left button came up: the press is over.</summary>
    public void Up()
    {
        _origin = null;
        _item = null;
    }

    /// <summary>The pointer moved to here.</summary>
    /// <param name="at">Where it is, in the same coordinates as the press.</param>
    /// <param name="leftPressed">Whether the left button is still down.</param>
    /// <param name="projectOpen">Whether a project view fills the main area, where nothing is dragged (AC 15).</param>
    /// <param name="board">The board, which says whether anything can be dropped on it.</param>
    public BoardDragStep Moved(Point at, bool leftPressed, bool projectOpen, BoardViewModel board)
    {
        ArgumentNullException.ThrowIfNull(board);

        if (!leftPressed || _origin is not { } origin || _item is not { } item || projectOpen)
        {
            return BoardDragStep.None;
        }

        var moved = at - origin;
        var (width, height) = (SystemParameters.MinimumHorizontalDragDistance, SystemParameters.MinimumVerticalDragDistance);

        if (Math.Abs(moved.X) < width && Math.Abs(moved.Y) < height)
        {
            return BoardDragStep.None;
        }

        if (BoardGesture.Refusal(board) is { } refusal)
        {
            if (_refused || (Math.Abs(moved.X) < width * RefusalDistance && Math.Abs(moved.Y) < height * RefusalDistance))
            {
                return BoardDragStep.None;
            }

            _refused = true;

            return new BoardDragStep(null, refusal);
        }

        Up();

        return new BoardDragStep(item, null);
    }
}
