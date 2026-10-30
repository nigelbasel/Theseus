using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using Theseus.App.Services;
using Theseus.App.ViewModels;
using Theseus.Core.Layout;

namespace Theseus.App;

/// <summary>
/// The one window. Its code-behind holds only what genuinely belongs to the visual tree: drag and
/// drop, which is a gesture, not a decision. Every decision lives one project down.
/// </summary>
public partial class MainWindow : Window
{
    private Point? _dragOrigin;
    private SettingsWindow? _settings;
    private ArrangeWindow? _arrange;
    private DayReportWindow? _report;
    private InstructionsWindow? _instructions;
    private SetupWindow? _setup;

    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();

        FitToWorkArea();

        // The remembered position is applied once the window has a handle, and saved as it closes.
        SourceInitialized += (_, _) => RestorePlacement();
        Closing += (_, _) => SavePlacement();

        AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnPreviewMouseDown), handledEventsToo: true);
        AddHandler(PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnPreviewMouseUp), handledEventsToo: true);
        AddHandler(PreviewMouseMoveEvent, new MouseEventHandler(OnPreviewMouseMove), handledEventsToo: true);

        DataContextChanged += (_, _) =>
        {
            WatchSettings();
            WatchArrange();
            WatchDayReport();
            WatchInstructions();
            WatchSetup();
        };
        Activated += (_, _) =>
        {
            (DataContext as ShellViewModel)?.SyncAccount();
            AllowMotion(true);
        };

        Deactivated += (_, _) => AllowMotion(false);
    }

    private BoardViewModel? Board => (DataContext as ShellViewModel)?.Board;

    /// <summary>
    /// Lets the cards move, or stops them. A board nobody is looking at should not hold the render
    /// thread awake, and a machine where animation has been turned off system-wide is saying not to.
    /// </summary>
    private void AllowMotion(bool active)
    {
        if (DataContext is ShellViewModel shell)
        {
            shell.IsMotionAllowed = active && SystemParameters.ClientAreaAnimation;
        }
    }

    private void WatchSettings()
    {
        if ((DataContext as ShellViewModel)?.Settings is not { } settings)
        {
            return;
        }

        settings.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(SettingsViewModel.IsOpen))
            {
                ShowSettings(settings);
            }
        };
    }

    private void ShowSettings(SettingsViewModel settings)
    {
        if (_settings is not null)
        {
            if (!settings.IsOpen)
            {
                _settings.Close();
            }

            return;
        }

        if (!settings.IsOpen)
        {
            return;
        }

        _settings = new SettingsWindow { Owner = this, DataContext = settings };
        _settings.Closed += (_, _) =>
        {
            _settings = null;
            settings.IsOpen = false;
        };

        _settings.ShowDialog();
    }

    private void WatchArrange()
    {
        if ((DataContext as ShellViewModel)?.Arrange is not { } arrange)
        {
            return;
        }

        arrange.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(ArrangeViewModel.IsOpen))
            {
                ShowArrange(arrange);
            }
        };
    }

    /// <summary>
    /// Shows the arranger, and never as a dialog. Windows move around underneath it while it is
    /// open, and the whole point is to look at what happened and try something else.
    /// </summary>
    private void ShowArrange(ArrangeViewModel arrange)
    {
        if (_arrange is not null)
        {
            if (arrange.IsOpen)
            {
                _arrange.Activate();
            }
            else
            {
                _arrange.Close();
            }

            return;
        }

        if (!arrange.IsOpen)
        {
            return;
        }

        _arrange = new ArrangeWindow { Owner = this, DataContext = arrange };
        _arrange.Closed += (_, _) =>
        {
            _arrange = null;
            arrange.IsOpen = false;
        };

        _arrange.Show();
    }

    private void WatchInstructions()
    {
        if ((DataContext as ShellViewModel)?.Instructions is not { } instructions)
        {
            return;
        }

        instructions.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(InstructionsViewModel.IsOpen))
            {
                ShowInstructions(instructions);
            }
        };
    }

    private void ShowInstructions(InstructionsViewModel instructions)
    {
        if (_instructions is not null)
        {
            if (instructions.IsOpen)
            {
                _instructions.Activate();
            }
            else
            {
                _instructions.Close();
            }

            return;
        }

        if (!instructions.IsOpen)
        {
            return;
        }

        _instructions = new InstructionsWindow { Owner = this, DataContext = instructions };
        _instructions.Closed += (_, _) =>
        {
            _instructions = null;
            instructions.IsOpen = false;
        };

        _instructions.Show();
    }

    private void WatchSetup()
    {
        if ((DataContext as ShellViewModel)?.Drawer is not { } drawer)
        {
            return;
        }

        drawer.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(DetailDrawerViewModel.IsEditingSetup))
            {
                ShowSetup(drawer);
            }
        };
    }

    /// <summary>
    /// Opens a card's setup as a dialog over the board, and closes it when Save or Cancel has been
    /// pressed. Closing it any other way - its close button, Alt+F4 - is Cancel.
    /// </summary>
    private void ShowSetup(DetailDrawerViewModel drawer)
    {
        if (!drawer.IsEditingSetup)
        {
            _setup?.Close();

            return;
        }

        if (_setup is not null)
        {
            return;
        }

        var setup = new SetupWindow { Owner = this, DataContext = DataContext };
        setup.Closed += (_, _) =>
        {
            _setup = null;

            if (drawer.IsEditingSetup)
            {
                drawer.CancelSetupCommand.Execute(null);
            }
        };

        _setup = setup;
        Dispatcher.BeginInvoke(() => setup.ShowDialog());
    }

    private void WatchDayReport()
    {
        if ((DataContext as ShellViewModel)?.DayReport is not { } report)
        {
            return;
        }

        report.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(DayReportViewModel.IsOpen))
            {
                ShowDayReport(report);
            }
        };
    }

    private void ShowDayReport(DayReportViewModel report)
    {
        if (_report is not null)
        {
            if (report.IsOpen)
            {
                _report.Activate();
            }
            else
            {
                _report.Close();
            }

            return;
        }

        if (!report.IsOpen)
        {
            return;
        }

        _report = new DayReportWindow { Owner = this, DataContext = report };
        _report.Closed += (_, _) =>
        {
            _report = null;
            report.IsOpen = false;
        };

        _report.Show();
    }

    /// <summary>
    /// Sizes the window to what the display can actually show. A fixed default is wrong on a scaled
    /// screen: the right-hand panel and the header controls end up beyond the edge of the desktop.
    /// Used only when there is nothing remembered, or when what was remembered is no longer usable.
    /// </summary>
    private void FitToWorkArea()
    {
        var area = SystemParameters.WorkArea;

        Width = Math.Min(Width, area.Width - 40);
        Height = Math.Min(Height, area.Height - 40);
        Left = area.Left + ((area.Width - Width) / 2);
        Top = area.Top + ((area.Height - Height) / 2);
    }

    /// <summary>
    /// Puts the window back where it was left, if that is somewhere it can still be seen.
    ///
    /// A saved position is a fact about a desktop that may not exist any more: undock a laptop and
    /// the second monitor's coordinates become empty space. So it is checked against the screens
    /// attached now, and brought back onto one when it no longer lands on any. An application that
    /// skips that check opens where nobody can see it, and there is no way back but the keyboard.
    /// </summary>
    private void RestorePlacement()
    {
        if ((DataContext as ShellViewModel)?.SettingsStore is not { } store)
        {
            return;
        }

        if (WindowPlacementFit.Onto(store.Read().MainWindow, Displays.WorkAreas()) is not { } placement)
        {
            return;
        }

        OwnWindowPlacement.Apply(new WindowInteropHelper(this).Handle, placement);
    }

    /// <summary>Remembers where the window is, so the next run opens where this one was left.</summary>
    private void SavePlacement()
    {
        if ((DataContext as ShellViewModel)?.SettingsStore is not { } store
            || OwnWindowPlacement.Capture(new WindowInteropHelper(this).Handle) is not { } placement)
        {
            return;
        }

        store.Write(store.Read() with { MainWindow = placement });
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e) =>
        _dragOrigin = e.GetPosition(this);

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e) => _dragOrigin = null;

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragOrigin is not { } origin)
        {
            return;
        }

        var moved = e.GetPosition(this) - origin;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (FindCard(e.OriginalSource as DependencyObject) is not { } card)
        {
            return;
        }

        _dragOrigin = null;
        DragDrop.DoDragDrop(this, card.Key, DragDropEffects.Move);
    }

    private void OnAdoptClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button)
        {
            return;
        }

        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    /// <summary>
    /// Opens a card in the drawer, unless the click landed on a control of its own. A disabled
    /// button does not swallow the click, so without this a chip that cannot act silently opens the
    /// drawer instead, which reads as the wrong thing happening rather than as nothing happening.
    /// </summary>
    private void OnCardClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: WorkstreamCardViewModel card }
            || IsWithinAControl(e.OriginalSource as DependencyObject)
            || DataContext is not ShellViewModel shell)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;

        shell.Board.ClickSelect(card, modifiers.HasFlag(ModifierKeys.Control), modifiers.HasFlag(ModifierKeys.Shift));
        shell.Clicked(card);
        e.Handled = true;
    }

    /// <summary>
    /// A right-click on a card outside the selection selects that card alone, so its menu acts on what
    /// was clicked; on a card inside the selection it leaves the selection for the menu to act on.
    /// </summary>
    private void OnCardRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WorkstreamCardViewModel { IsSelected: false } card }
            && DataContext is ShellViewModel shell)
        {
            shell.Board.ClickSelect(card, toggle: false, extend: false);
            shell.Clicked(card);
        }
    }

    /// <summary>A double-click opens the card's properties, as Alt+Enter and its menu do.</summary>
    private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2
            && sender is FrameworkElement { DataContext: WorkstreamCardViewModel card }
            && !IsWithinAControl(e.OriginalSource as DependencyObject))
        {
            card.PropertiesCommand?.Execute(null);
            e.Handled = true;
        }
    }

    private Point? _bandStart;
    private bool _bandAdds;
    private bool _banding;

    /// <summary>Starts a rubber band, when the press is on empty board rather than on a card or a control.</summary>
    private void OnBoardMouseDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;

        if (FindCard(source) is not null || IsWithinAControl(source) || IsWithin<System.Windows.Controls.Primitives.ScrollBar>(source)
            || IsWithin<System.Windows.Controls.Primitives.Thumb>(source))
        {
            return;
        }

        _bandStart = e.GetPosition(BandLayer);
        _bandAdds = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        _banding = false;
    }

    private void OnBoardMouseMove(object sender, MouseEventArgs e)
    {
        if (_bandStart is not { } start || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var here = e.GetPosition(BandLayer);

        if (!_banding && Math.Abs(here.X - start.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(here.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (!_banding)
        {
            _banding = true;
            BoardSurface.CaptureMouse();
            Band.Visibility = Visibility.Visible;
        }

        var area = new Rect(start, here);
        Canvas.SetLeft(Band, area.X);
        Canvas.SetTop(Band, area.Y);
        Band.Width = area.Width;
        Band.Height = area.Height;
    }

    private void OnBoardMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_bandStart is not { } start || DataContext is not ShellViewModel shell)
        {
            _bandStart = null;

            return;
        }

        if (_banding)
        {
            var area = new Rect(start, e.GetPosition(BandLayer));

            shell.Board.BandSelect([.. CardsWithin(area)], _bandAdds);
            Band.Visibility = Visibility.Collapsed;
            BoardSurface.ReleaseMouseCapture();
            e.Handled = true;
        }
        else if (!_bandAdds)
        {
            shell.Board.ClearSelection();
        }

        _bandStart = null;
        _banding = false;
    }

    /// <summary>Moves the edge between a column and the next by the pixels dragged, as a share of the width.</summary>
    private void OnColumnEdgeDrag(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: StageHeaderViewModel header } edge
            || DataContext is not ShellViewModel shell
            || FindAncestor<Controls.StageColumnsPanel>(edge) is not { } panel)
        {
            return;
        }

        var openWidth = panel.ActualWidth - (shell.Board.StageHeaders.Count(other => other.IsCollapsed) * Controls.StageColumnsPanel.FoldedWidth);

        if (openWidth > 0)
        {
            shell.Board.ResizeColumns(header, e.HorizontalChange * shell.Board.OpenColumnWeight / openWidth);
        }
    }

    /// <summary>A double-click on a column edge makes every column even again.</summary>
    private void OnColumnEdgeDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && DataContext is ShellViewModel shell)
        {
            shell.Board.ResetColumnWidths();
            e.Handled = true;
        }
    }

    /// <summary>Sets a row's height to where its lower edge has been dragged.</summary>
    private void OnRowEdgeDrag(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: BoardRowViewModel row } edge
            || DataContext is not ShellViewModel shell
            || FindAncestor<DockPanel>(edge) is not { } rowPanel
            || rowPanel.Children.OfType<ItemsControl>().FirstOrDefault() is not { } cells)
        {
            return;
        }

        var from = double.IsNaN(row.Height) ? cells.ActualHeight : row.Height;
        shell.Board.SetRowHeight(row, from + e.VerticalChange);
    }

    /// <summary>A double-click on a row's lower edge lets it fit its cards again.</summary>
    private void OnRowEdgeDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: BoardRowViewModel row } && DataContext is ShellViewModel shell)
        {
            shell.Board.ResetRowHeight(row);
            e.Handled = true;
        }
    }

    private void OnSizeDragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) =>
        (DataContext as ShellViewModel)?.Board.RememberSizes();

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        for (var node = source is null ? null : ParentOf(source); node is not null; node = ParentOf(node))
        {
            if (node is T found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>The cards on show whose outline the rubber band touches.</summary>
    private IEnumerable<WorkstreamCardViewModel> CardsWithin(Rect area)
    {
        var pending = new Stack<DependencyObject>([BoardSurface]);

        while (pending.TryPop(out var element))
        {
            if (element is FrameworkElement { Tag: "Card", DataContext: WorkstreamCardViewModel card, IsVisible: true } face)
            {
                // The band's layer sits beside the board, not above it, so this is a transform between two
                // visuals of the same window, not one to an ancestor.
                var bounds = face.TransformToVisual(BandLayer).TransformBounds(new Rect(face.RenderSize));

                if (bounds.IntersectsWith(area))
                {
                    yield return card;
                }

                continue;
            }

            for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(element); index++)
            {
                pending.Push(System.Windows.Media.VisualTreeHelper.GetChild(element, index));
            }
        }
    }

    private static bool IsWithin<T>(DependencyObject? source)
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

    private static bool IsWithinAControl(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }

            source = ParentOf(source);
        }

        return false;
    }

    /// <summary>
    /// Lets the wheel through to the board when the cell under the pointer has nowhere to scroll.
    ///
    /// A WPF ScrollViewer marks a wheel event handled whether or not it could act on it, so a cell
    /// holding two cards swallows every scroll over it and the board behind never moves. That is
    /// harmless while the cells are the thing being scrolled, and became the bug the moment the board
    /// itself started scrolling underneath them.
    ///
    /// A cell that can still scroll in the direction asked for keeps the event, so scrolling a long
    /// cell still works and only reaches the board once the cell is at its end.
    /// </summary>
    /// <summary>
    /// Drops the overflow menu under the button that owns it. A context menu is normally the
    /// right-click gesture; here it is the whole point of the button, so it opens on a left click and
    /// below rather than at the pointer.
    /// </summary>
    /// <summary>
    /// Opens one folded card. Handled here rather than bound to a command because it acts on the card
    /// it sits in and nothing else, and a command would have to be given the card to act on.
    /// </summary>
    private void OnExpandCard(object sender, RoutedEventArgs e) => FoldCard(sender, e, folded: false);

    /// <summary>Folds one card down to its title, from the button beside its status.</summary>
    private void OnCollapseCard(object sender, RoutedEventArgs e) => FoldCard(sender, e, folded: true);

    private void FoldCard(object sender, RoutedEventArgs e, bool folded)
    {
        if (sender is FrameworkElement { DataContext: WorkstreamCardViewModel card } && DataContext is ShellViewModel shell)
        {
            shell.Board.Fold(card, folded);
        }

        // The card is inside a click-to-open-the-drawer surface, so the click stops here.
        e.Handled = true;
    }

    private void OnCardMouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: WorkstreamCardViewModel card } && DataContext is ShellViewModel shell)
        {
            shell.Chains?.Highlight(card.Key);
        }
    }

    private void OnCardMouseLeave(object sender, MouseEventArgs e)
    {
        if (DataContext is ShellViewModel shell)
        {
            shell.Chains?.Highlight(null);
        }
    }

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    private void OnAbout(object sender, RoutedEventArgs e)
    {
        (DataContext as ShellViewModel)?.Help.Check();
        new AboutWindow { Owner = this, DataContext = DataContext }.ShowDialog();
    }

    private void OnOverflowClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.ContextMenu is not { } menu)
        {
            return;
        }

        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnCellMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer cell)
        {
            return;
        }

        var canScrollOn = cell.ScrollableHeight > 0
                          && ((e.Delta < 0 && cell.VerticalOffset < cell.ScrollableHeight)
                              || (e.Delta > 0 && cell.VerticalOffset > 0));

        if (canScrollOn)
        {
            return;
        }

        // Scroll the board directly rather than re-raising the event for it to catch.
        //
        // The first attempt raised a fresh wheel event on this ScrollViewer's Parent, which is the
        // textbook trick and did nothing here: a ScrollViewer built from a ControlTemplate has no
        // logical parent, so there was nothing to raise it on and the gesture still died in the cell.
        // Walking the visual tree and moving the ancestor by hand depends on no routing at all.
        if (Ancestor(cell) is not { } board)
        {
            return;
        }

        e.Handled = true;
        board.ScrollToVerticalOffset(board.VerticalOffset - e.Delta);
    }

    /// <summary>The first ScrollViewer above this one in the visual tree, if there is one.</summary>
    private static ScrollViewer? Ancestor(DependencyObject from)
    {
        for (var node = VisualTreeHelper.GetParent(from); node is not null;
             node = VisualTreeHelper.GetParent(node))
        {
            if (node is ScrollViewer found)
            {
                return found;
            }
        }

        return null;
    }

    private void OnColumnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.StringFormat) && Board?.CanDrag == true
            ? DragDropEffects.Move
            : DragDropEffects.None;

        e.Handled = true;
    }

    private void OnColumnDrop(object sender, DragEventArgs e)
    {
        if (sender is not ItemsControl { DataContext: ColumnViewModel column } lane
            || e.Data.GetData(DataFormats.StringFormat) is not string key)
        {
            return;
        }

        // The cell carries both axes, so a drop says stage and area at once. Dragging within a row
        // is the same gesture as dragging across one; only the cell landed in is different.
        Board?.Move(key, column.Column, column.Area, DropIndex(lane, column, e.GetPosition(lane)));
        e.Handled = true;
    }

    /// <summary>
    /// Where in the lane a drop landed. A card is displaced by one that lands on its upper half and
    /// keeps its place against one that lands on its lower half, which is what makes dropping onto a
    /// boundary do the thing the pointer is nearest to rather than the thing it is over.
    /// </summary>
    private static int DropIndex(ItemsControl lane, ColumnViewModel column, Point position)
    {
        for (var index = 0; index < column.Cards.Count; index++)
        {
            if (lane.ItemContainerGenerator.ContainerFromIndex(index) is not FrameworkElement container)
            {
                continue;
            }

            var top = container.TranslatePoint(new Point(0, 0), lane).Y;

            if (position.Y < top + (container.ActualHeight / 2))
            {
                return index;
            }
        }

        return column.Cards.Count;
    }

    private static WorkstreamCardViewModel? FindCard(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: WorkstreamCardViewModel card })
            {
                return card;
            }

            source = ParentOf(source);
        }

        return null;
    }

    /// <summary>
    /// The element above this one. A click on text made of runs reports the run, which is not a
    /// visual, and asking the visual tree for its parent throws; on 2026-10-30 that closed the board.
    /// </summary>
    private static DependencyObject? ParentOf(DependencyObject source) =>
        source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
            ? System.Windows.Media.VisualTreeHelper.GetParent(source)
            : LogicalTreeHelper.GetParent(source);
}
