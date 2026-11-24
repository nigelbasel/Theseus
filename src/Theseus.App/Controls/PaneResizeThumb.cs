using System.Windows;
using System.Windows.Controls.Primitives;

namespace Theseus.App.Controls;

/// <summary>
/// The inner edge of a side panel or the drawer: dragging it resizes the pane, held between the pane's MinWidth and
/// MaxWidth (baseline W39; finding D13; checklist G115). A pane at the left of the board grows as its right edge is
/// dragged right; one at the right grows as its left edge is dragged left.
/// </summary>
public sealed class PaneResizeThumb : Thumb
{
    /// <summary>The pane whose width the edge sets.</summary>
    public static readonly DependencyProperty PaneProperty = DependencyProperty.Register(
        nameof(Pane), typeof(FrameworkElement), typeof(PaneResizeThumb));

    /// <summary>Whether the pane sits at the right of the board, so dragging left widens it.</summary>
    public static readonly DependencyProperty IsOnTheRightProperty = DependencyProperty.Register(
        nameof(IsOnTheRight), typeof(bool), typeof(PaneResizeThumb));

    /// <summary>Creates the edge.</summary>
    public PaneResizeThumb() => DragDelta += OnDragDelta;

    /// <summary>The pane whose width the edge sets.</summary>
    public FrameworkElement? Pane
    {
        get => (FrameworkElement?)GetValue(PaneProperty);
        set => SetValue(PaneProperty, value);
    }

    /// <summary>Whether the pane sits at the right of the board, so dragging left widens it.</summary>
    public bool IsOnTheRight
    {
        get => (bool)GetValue(IsOnTheRightProperty);
        set => SetValue(IsOnTheRightProperty, value);
    }

    private void OnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (Pane is not { } pane)
        {
            return;
        }

        double from = double.IsNaN(pane.Width) ? pane.ActualWidth : pane.Width;
        double change = IsOnTheRight ? -e.HorizontalChange : e.HorizontalChange;

        pane.Width = Math.Clamp(from + change, pane.MinWidth, pane.MaxWidth);
        e.Handled = true;
    }
}
