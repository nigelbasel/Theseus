using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Theseus.App.ViewModels;

namespace Theseus.App.Views;

/// <summary>
/// The project view (1.1d AC 15 to 37). Its code draws the connectors from where the boxes were laid out, keeps the
/// Properties pane's width, and keeps the scroll offset across a rebuild of the same project.
/// </summary>
public partial class ProjectView : UserControl
{
    private bool _drawQueued;

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

        if (DataContext is not ProjectViewModel view)
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
                ? ConnectorGeometry.HandsOver(from, to)
                : ConnectorGeometry.ThenWorkedOn(from, to);
            var tag = $"{link.Kind}:{link.To.Name}";
            var line = new Path { Data = figure.LineGeometry(), Tag = tag, StrokeThickness = link.Kind == ConnectorKind.HandsOver ? 2 : 1.5 };
            var arrow = new Path { Data = figure.ArrowGeometry(), Tag = tag + ":arrow" };

            // StrokeDashArray counts in stroke widths: the mockup's "2 4" at 1.5 px is 1.33 2.67.
            if (link.Kind == ConnectorKind.ThenWorkedOn)
            {
                line.StrokeDashArray = [1.33, 2.67];
            }

            line.SetResourceReference(Shape.StrokeProperty, "ConnectorBrush");
            arrow.SetResourceReference(Shape.FillProperty, "ConnectorBrush");
            Connectors.Children.Add(line);
            Connectors.Children.Add(arrow);
        }
    }

    private Dictionary<PieceNodeViewModel, Rect> Boxes()
    {
        var boxes = new Dictionary<PieceNodeViewModel, Rect>();

        foreach (var box in Descendants(RootList).OfType<Border>().Where(border => border.Name == "Box"))
        {
            if (box.DataContext is PieceNodeViewModel node)
            {
                boxes[node] = new Rect(box.TranslatePoint(new Point(0, 0), Connectors), box.RenderSize);
            }
        }

        return boxes;
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

    private void OnFlowSizeChanged(object sender, SizeChangedEventArgs e) => QueueDraw();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not ProjectViewModel view)
        {
            return;
        }

        PaneColumn.Width = new GridLength(ProjectPaneWidth.Clamp(view.PaneWidth, Split.ActualWidth > 0 ? Split.ActualWidth : double.PositiveInfinity));

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

    /// <summary>
    /// Puts keyboard focus in the view, unless it is there already, so its own keys (Esc, Enter on a header) reach it.
    /// A click on a box's name, meta, status or a strip chip runs a mouse binding and would otherwise leave focus where
    /// it was (1.1d review of PR 2, finding 1). Returns whether focus is now in the view.
    /// </summary>
    public bool FocusView() => IsKeyboardFocusWithin || Focus() || FocusManager.GetFocusedElement(FocusManager.GetFocusScope(this)) == this;

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e) => FocusView();

    private void OnSplitSizeChanged(object sender, SizeChangedEventArgs e)
    {
        PaneColumn.MaxWidth = ProjectPaneWidth.Maximum(Split.ActualWidth);
        QueueDraw();
    }

    private void OnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (DataContext is ProjectViewModel view)
        {
            view.RememberPaneWidth(ProjectPaneWidth.Clamp(PaneColumn.ActualWidth, Split.ActualWidth));
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
