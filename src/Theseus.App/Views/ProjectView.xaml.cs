using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Theseus.App.ViewModels;
using Theseus.Core.Settings;

namespace Theseus.App.Views;

/// <summary>
/// The project view (1.1d AC 15 to 54). Its code draws the connectors from where the boxes were laid out, keeps the
/// Properties pane's width, docks the pane or steps it aside as the view's width allows, and keeps the scroll offset
/// across a rebuild of the same project.
/// </summary>
public partial class ProjectView : UserControl
{
    private bool _drawQueued;
    private ProjectViewGrouping _grouping;

    /// <summary>Creates the view.</summary>
    public ProjectView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Draws every connector as Path elements on the Connectors canvas, from each box's rectangle in it (AC 25). Run
    /// after each layout pass that moves or resizes a box; public so a test can run it after laying the view out.
    /// </summary>
    public void DrawConnectors()
    {
        Connectors.Children.Clear();

        if (DataContext is not ProjectViewModel { IsGroupedByFlow: true } view)
        {
            return;
        }

        var boxes = Boxes();

        foreach (var link in view.Connectors)
        {
            if (!boxes.TryGetValue(link.From, out var from) || !boxes.TryGetValue(link.To, out var to))
            {
                continue;
            }

            var figure = link.Kind == ConnectorKind.HandsOver
                ? ConnectorGeometry.HandsOver(from, to, link.From.HandOverInset)
                : ConnectorGeometry.ThenWorkedOn(from, to);
            var tag = $"{link.Kind}:{link.To.Name}";
            var line = new Path { Data = figure.LineGeometry(), Tag = tag, StrokeThickness = link.Kind == ConnectorKind.HandsOver ? 2 : 1.5 };
            var arrow = new Path { Data = figure.ArrowGeometry(), Tag = tag + ":arrow" };

            // StrokeDashArray counts in stroke widths: the mockup's "2 4" at 1.5 px is 1.33 2.67.
            if (link.Kind == ConnectorKind.ThenWorkedOn)
            {
                line.StrokeDashArray = [1.33, 2.67];
                Connectors.Children.Add(ThenWorkedOnLabel(from, to, tag));
            }

            line.SetResourceReference(Shape.StrokeProperty, "ConnectorBrush");
            arrow.SetResourceReference(Shape.FillProperty, "ConnectorBrush");
            Connectors.Children.Add(line);
            Connectors.Children.Add(arrow);
        }
    }

    // Beside the dotted line, in the gap between the two boxes, so it crosses neither (1.1f D20, D22).
    private static TextBlock ThenWorkedOnLabel(Rect from, Rect to, string tag)
    {
        const string Text = "then the same session moved on to";
        var label = new TextBlock
        {
            Text = Text,
            FontSize = 13,
            Tag = tag + ":label",
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = Math.Max(60, to.Width - ConnectorGeometry.ThenWorkedOnInset - 16),
            ToolTip = Text,
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        Canvas.SetLeft(label, from.Left + ConnectorGeometry.ThenWorkedOnInset + 10);
        Canvas.SetTop(label, to.Top - 30);

        return label;
    }

    private Dictionary<PieceNodeViewModel, Rect> Boxes()
    {
        var boxes = new Dictionary<PieceNodeViewModel, Rect>();

        foreach (var box in Descendants(RootList).OfType<Border>().Where(border => border.Name == "Box"))
        {
            if (box.DataContext is PieceNodeViewModel node && !UnderCollapsed(box))
            {
                boxes[node] = new Rect(box.TranslatePoint(new Point(0, 0), Connectors), box.RenderSize);
            }
        }

        return boxes;
    }

    // A box under a folded round or run keeps the place it had when last shown, so it is left out.
    private static bool UnderCollapsed(DependencyObject element)
    {
        for (var at = element; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (at is UIElement { Visibility: not Visibility.Visible })
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            yield return child;

            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }

    // Coalesced, so a fold that resizes many boxes draws once, after the layout pass that moved them.
    private void QueueDraw()
    {
        if (_drawQueued)
        {
            return;
        }

        _drawQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            _drawQueued = false;
            DrawConnectors();
        });
    }

    private void OnBoxSizeChanged(object sender, SizeChangedEventArgs e) => QueueDraw();

    // Hovering a box highlights its objective's chip, and a chip its boxes (1.1f I45).
    private void OnBoxEnter(object sender, MouseEventArgs e)
    {
        if (DataContext is ProjectViewModel view && sender is FrameworkElement { DataContext: PieceNodeViewModel node })
        {
            view.HighlightObjective(node.Piece.ObjectiveNumber);
        }
    }

    private void OnObjectiveChipEnter(object sender, MouseEventArgs e)
    {
        if (DataContext is ProjectViewModel view && sender is FrameworkElement { DataContext: RoundObjectiveViewModel chip })
        {
            view.HighlightObjective(chip.Objective.Number);
        }
    }

    private void OnHoverLeave(object sender, MouseEventArgs e) => (DataContext as ProjectViewModel)?.HighlightObjective(null);

    private void OnFlowSizeChanged(object sender, SizeChangedEventArgs e) => QueueDraw();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ProjectViewModel previous)
        {
            previous.PropertyChanged -= OnViewChanged;
        }

        if (e.NewValue is not ProjectViewModel view)
        {
            return;
        }

        view.PropertyChanged += OnViewChanged;
        _grouping = view.Grouping;

        PaneColumn.Width = new GridLength(ProjectPaneWidth.Clamp(view.PaneWidth, ActualWidth > 0 ? ActualWidth : double.PositiveInfinity));

        if (ActualWidth > 0)
        {
            view.PaneFits(ProjectPaneWidth.Docks(ActualWidth));
        }

        Dock(view, changed: true);

        // A rebuild of the same project keeps where it was scrolled to (AC 36); another project starts at the top.
        if (e.OldValue is ProjectViewModel old && string.Equals(old.Name, view.Name, StringComparison.OrdinalIgnoreCase))
        {
            var (across, down) = (Flow.HorizontalOffset, Flow.VerticalOffset);

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                Flow.ScrollToHorizontalOffset(across);
                Flow.ScrollToVerticalOffset(down);
            });
        }
        else
        {
            Flow.ScrollToHome();
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () => FocusView());
        }

        QueueDraw();
    }

    // A switch of grouping lays out other boxes entirely, so it starts at the top and the connectors are drawn again, or
    // cleared, at once (PR 4 review, finding 3).
    private void OnViewChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectViewModel.IsGroupedByFlow) && sender is ProjectViewModel view && view.Grouping != _grouping)
        {
            _grouping = view.Grouping;
            Flow.ScrollToHome();
            QueueDraw();
        }
    }

    /// <summary>
    /// Puts keyboard focus in the view, unless it is there already, so its own keys (Esc, Enter on a header) reach it.
    /// A click on a box's name, meta, status or a strip chip runs a mouse binding and would otherwise leave focus where
    /// it was (1.1d review of PR 2, finding 1). Returns whether focus is now in the view.
    /// </summary>
    public bool FocusView() => IsKeyboardFocusWithin || Focus() || FocusManager.GetFocusedElement(FocusManager.GetFocusScope(this)) == this;

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e) => FocusView();

    private void OnSplitSizeChanged(object sender, SizeChangedEventArgs e) => QueueDraw();

    // The view's own width, not the split's: the split's column minimums keep it at least 726 px wide however narrow the
    // view, so its size would never say the pane no longer fits.
    private void OnViewSizeChanged(object sender, SizeChangedEventArgs e)
    {
        PaneColumn.MaxWidth = ProjectPaneWidth.Maximum(ActualWidth);

        if (DataContext is ProjectViewModel view)
        {
            var docked = view.IsPaneDocked;

            view.PaneFits(ProjectPaneWidth.Docks(ActualWidth));
            Dock(view, view.IsPaneDocked != docked);
        }

        QueueDraw();
    }

    /// <summary>
    /// Docks the pane beside the flow, at its remembered width when it has just come back (AC 53), or gives the flow the
    /// whole width while Properties is an overlay over it (AC 52). The overlay's place is the pane's style.
    /// </summary>
    private void Dock(ProjectViewModel view, bool changed)
    {
        if (view.IsPaneDocked)
        {
            FlowColumn.MinWidth = ProjectPaneWidth.MinimumFlow;
            SplitterColumn.Width = new GridLength(ProjectPaneWidth.Splitter);
            PaneColumn.MinWidth = ProjectPaneWidth.Minimum;

            if (changed && ActualWidth > 0)
            {
                PaneColumn.Width = new GridLength(ProjectPaneWidth.Clamp(view.PaneWidth, ActualWidth));
            }
        }
        else
        {
            FlowColumn.MinWidth = 0;
            SplitterColumn.Width = new GridLength(0);
            PaneColumn.MinWidth = 0;
            PaneColumn.Width = new GridLength(0);
        }
    }

    private void OnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is ProjectViewModel { IsPaneDocked: true } view)
        {
            view.RememberPaneWidth(ProjectPaneWidth.Clamp(PaneColumn.ActualWidth, ActualWidth));
        }
    }

    private void OnSplitterDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ProjectViewModel view)
        {
            PaneColumn.Width = new GridLength(view.ResetPaneWidth());
            e.Handled = true;
        }
    }
}
