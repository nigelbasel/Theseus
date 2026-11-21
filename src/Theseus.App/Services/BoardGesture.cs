using System.Windows;
using Theseus.App.ViewModels;

namespace Theseus.App.Services;

/// <summary>
/// What a press, a drag and a drop on the board mean (1.1d AC 41, 42), apart from the window, so they can be tested
/// against real elements: which card or project card a press is on, whether it starts a rubber band, what a drag
/// carries, and what a drop does or why it is refused.
/// </summary>
public static class BoardGesture
{
    /// <summary>What a project card's drag payload starts with, so a drop can tell it from a card's key.</summary>
    public const string ProjectPrefix = "project:";

    /// <summary>The card or project card an element belongs to, or null when it is on neither.</summary>
    public static IBoardCard? ItemAt(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: IBoardCard item })
            {
                return item;
            }

            source = ParentOf(source);
        }

        return null;
    }

    /// <summary>Whether a press here starts a rubber band: only on empty board, not on a card or a control.</summary>
    public static bool StartsBand(DependencyObject? source) =>
        ItemAt(source) is null
        && !IsWithin<System.Windows.Controls.Primitives.ButtonBase>(source)
        && !IsWithin<System.Windows.Controls.Primitives.ScrollBar>(source)
        && !IsWithin<System.Windows.Controls.Primitives.Thumb>(source);

    /// <summary>Why nothing dragged can be dropped on this board just now, or null when it can.</summary>
    public static string? Refusal(BoardViewModel board)
    {
        ArgumentNullException.ThrowIfNull(board);

        return board.CanDrag
            ? null
            : "Cards and projects are moved only along stages or areas: choose View › Columns by › Stage, or View › Rows by › Area.";
    }

    /// <summary>
    /// Drops what a drag carried on a cell, at an index among its cards. Returns what the status bar says when the drop
    /// also tried a derived axis (1.1e AC 3), or null.
    /// </summary>
    public static string? Drop(BoardViewModel board, string payload, ColumnViewModel cell, int index)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(cell);

        return payload.StartsWith(ProjectPrefix, StringComparison.Ordinal)
            ? board.MoveProject(payload[ProjectPrefix.Length..], cell.Column, cell.Area)
            : board.Move(payload, cell.Column, cell.Area, index);
    }

    /// <summary>Whether an element is inside one of a type.</summary>
    public static bool IsWithin<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T)
            {
                return true;
            }

            source = ParentOf(source);
        }

        return false;
    }

    /// <summary>
    /// The element above this one. A click on text made of runs reports the run, which is not a
    /// visual, and asking the visual tree for its parent throws; on 2026-10-30 that closed the board.
    /// </summary>
    public static DependencyObject? ParentOf(DependencyObject source) =>
        source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
            ? System.Windows.Media.VisualTreeHelper.GetParent(source)
            : LogicalTreeHelper.GetParent(source);
}
