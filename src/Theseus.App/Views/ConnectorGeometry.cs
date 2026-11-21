using System.Windows;
using Theseus.App.ViewModels;

namespace Theseus.App.Views;

/// <summary>
/// Where the project view's connectors run, from the boxes' rectangles in the Connectors canvas (1.1d AC 25, after the
/// mockup's drawEdges): pure, so it is tested without a window.
/// </summary>
public static class ConnectorGeometry
{
    /// <summary>The arrowhead's length and width.</summary>
    public const double ArrowSize = 7;

    /// <summary>How far in from a parent's left edge a hand-over drops, and how far down the child it turns in.</summary>
    public const double HandOverInset = 18;

    /// <summary>How far in a branch starts from its parent's left edge.</summary>
    public const double BranchIndent = 34;

    /// <summary>
    /// How far in a parallel branch starts: room left of the hand-over line for the brace and its label (1.1e AC 6d).
    /// </summary>
    public const double ParallelBranchIndent = 56;

    /// <summary>How far in from a parallel parent's left edge its hand-overs drop, right of its brace (1.1e AC 6d).</summary>
    public const double ParallelHandOverInset = 40;

    /// <summary>How far down a child's top the hand-over meets it.</summary>
    public const double HandOverDrop = 16;

    /// <summary>How far in from the left a then worked on line runs.</summary>
    public const double ThenWorkedOnInset = 30;

    /// <summary>
    /// Hands work to: down from (parent.Left + 18, parent.Bottom) to the child's top + 16, then right to the child's left
    /// edge.
    /// </summary>
    /// <param name="parent">The parent's box.</param>
    /// <param name="child">The child's box.</param>
    /// <param name="inset">How far in from the parent's left edge the line drops: 40 for a parallel parent (AC 6d).</param>
    public static ConnectorFigure HandsOver(Rect parent, Rect child, double inset = HandOverInset)
    {
        var x = parent.Left + inset;
        var y = child.Top + HandOverDrop;
        var tip = new Point(child.Left, y);

        return new ConnectorFigure(ConnectorKind.HandsOver,
            [new Point(x, parent.Bottom), new Point(x, y), new Point(tip.X - ArrowSize, y)],
            [tip, new Point(tip.X - ArrowSize, y - ArrowSize / 2), new Point(tip.X - ArrowSize, y + ArrowSize / 2)]);
    }

    /// <summary>Then worked on: straight down from (a.Left + 30, a.Bottom) to b's top.</summary>
    public static ConnectorFigure ThenWorkedOn(Rect from, Rect to)
    {
        var x = from.Left + ThenWorkedOnInset;
        var tip = new Point(x, to.Top);

        return new ConnectorFigure(ConnectorKind.ThenWorkedOn,
            [new Point(x, from.Bottom), new Point(x, tip.Y - ArrowSize)],
            [tip, new Point(x - ArrowSize / 2, tip.Y - ArrowSize), new Point(x + ArrowSize / 2, tip.Y - ArrowSize)]);
    }
}
