using System.ComponentModel;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Theseus.App.ViewModels;
using Theseus.Core.Settings;

namespace Theseus.App.Views;

/// <summary>
/// The project view (1.1d AC 15 to 54). Its code keeps the Properties pane's width, docks the pane or steps it aside as the view's width allows, and keeps the scroll offset
/// across a rebuild of the same project.
/// </summary>
public partial class ProjectView : UserControl
{
    private ProjectViewGrouping _grouping;
    private bool _tabsHadFocus;

    /// <summary>Creates the view.</summary>
    public ProjectView()
    {
        InitializeComponent();
        InputBindings.Add(new KeyBinding(new OutsideTextBoxCommand(this, () => (DataContext as ProjectViewModel)?.UndoClearUpCommand),
            Key.Z, ModifierKeys.Control));
        DataContextChanged += OnDataContextChanged;
        PreviewMouseMove += (_, e) => QuickToolTips(e.OriginalSource as DependencyObject);
    }

    /// <summary>How soon a tooltip in the view shows (I43).</summary>
    public const int ToolTipDelay = 300;

    // ToolTipService.InitialShowDelay is not inherited, and boxes, stages and links are made at run time, so each element
    // under the pointer gets the short delay before WPF's tooltip timer reads it.
    internal void QuickToolTips(DependencyObject? element)
    {
        for (var at = element; at is not null && !ReferenceEquals(at, this);
             at = at is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(at) : LogicalTreeHelper.GetParent(at))
        {
            if (ToolTipService.GetToolTip(at) is not null && ToolTipService.GetInitialShowDelay(at) != ToolTipDelay)
            {
                ToolTipService.SetInitialShowDelay(at, ToolTipDelay);
            }
        }
    }

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

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ProjectViewModel previous)
        {
            previous.PropertyChanged -= OnViewChanged;
            previous.AllWaitsRequested -= OnAllWaitsRequested;
        }

        // This one control shows every project, and its automation peers keep the children they last gave a client: a UI
        // Automation read of one project's tab got the previous project's work and counts (project-view-scale, 2026-11-24).
        if (!string.Equals((e.OldValue as ProjectViewModel)?.Name, (e.NewValue as ProjectViewModel)?.Name, StringComparison.OrdinalIgnoreCase))
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ForgetAutomationChildren);
        }

        if (e.NewValue is not ProjectViewModel view)
        {
            return;
        }

        view.PropertyChanged += OnViewChanged;
        view.AllWaitsRequested += OnAllWaitsRequested;
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

            // An arrow key in the tab strip opened this project: the rebuilt strip keeps the focus, so the next arrow works.
            bool fromTabs = _tabsHadFocus;
            _tabsHadFocus = false;
            Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
            {
                if (!(fromTabs && Tabs.ItemContainerGenerator.ContainerFromItem(Tabs.SelectedItem) is ListBoxItem tab && tab.Focus()))
                {
                    FocusView();
                }
            });
        }
    }

    // A switch of grouping lays out other boxes entirely, so it starts at the top (PR 4 review, finding 3).
    private void OnViewChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProjectViewModel.IsGroupedByFlow) && sender is ProjectViewModel view && view.Grouping != _grouping)
        {
            _grouping = view.Grouping;
            Flow.ScrollToHome();
        }
    }

    /// <summary>
    /// Puts keyboard focus in the view, unless it is there already, so its own keys (Esc, Enter on a header) reach it.
    /// A click on a box's name, meta, status or a strip chip runs a mouse binding and would otherwise leave focus where
    /// it was (1.1d review of PR 2, finding 1). Returns whether focus is now in the view.
    /// </summary>
    public bool FocusView() => IsKeyboardFocusWithin || Focus() || FocusManager.GetFocusedElement(FocusManager.GetFocusScope(this)) == this;

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e) => FocusView();

    // The view's own width, not the split's: the split's column minimums keep it at least 728 px wide however narrow the
    // view, so its size would never say the pane no longer fits.
    private void OnViewSizeChanged(object sender, SizeChangedEventArgs e)
    {
        PaneColumn.MaxWidth = ProjectPaneWidth.Maximum(ActualWidth);
        WhereScroller.MaxHeight = WhereShare.MaxHeight(ActualHeight);

        if (DataContext is ProjectViewModel view)
        {
            var docked = view.IsPaneDocked;

            view.PaneFits(ProjectPaneWidth.Docks(ActualWidth));
            Dock(view, view.IsPaneDocked != docked);
        }
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

    // The arrow keys move the splitter without a drag, so its width is kept on their release too (B17).
    private void OnSplitterKeyUp(object sender, KeyEventArgs e) => OnSplitterDragCompleted(sender, null!);

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e) => _tabsHadFocus = Tabs.IsKeyboardFocusWithin;

    private void OnAllWaitsRequested(object? sender, WaitsChecklistViewModel list) =>
        new WaitsWindow(list) { Owner = Window.GetWindow(this) }.ShowDialog();

    internal void ForgetAutomationChildren()
    {
        var pending = new Stack<DependencyObject>([this]);

        while (pending.TryPop(out var element))
        {
            if (element is UIElement ui && UIElementAutomationPeer.FromElement(ui) is { } peer)
            {
                peer.ResetChildrenCache();
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
            {
                pending.Push(VisualTreeHelper.GetChild(element, index));
            }
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
