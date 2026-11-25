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
    private readonly BoardPress _press = new();
    private SettingsWindow? _settings;
    private ArrangeWindow? _arrange;
    private DayReportWindow? _report;
    private InstructionsWindow? _instructions;
    private SetupWindow? _setup;
    private NewWorkstreamWindow? _composer;

    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();

        FitToWorkArea();

        // The mouse's back and forward buttons go through the same history as ◀ Back and Forward ▶ (1.1f I46).
        PreviewMouseDown += (_, e) =>
        {
            var command = e.ChangedButton switch
            {
                System.Windows.Input.MouseButton.XButton1 => (DataContext as ShellViewModel)?.GoBackCommand,
                System.Windows.Input.MouseButton.XButton2 => (DataContext as ShellViewModel)?.GoForwardCommand,
                _ => null,
            };

            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
                e.Handled = true;
            }
        };

        // The remembered position is applied once the window has a handle, and saved as it closes.
        SourceInitialized += (_, _) => RestorePlacement();
        Closing += (_, _) => SavePlacement();

        // Screens change under a running window too: monitors sleep and wake, and a dock comes and goes.
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;
        Closed += (_, _) => Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;

        AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnPreviewMouseDown), handledEventsToo: true);
        AddHandler(PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnPreviewMouseUp), handledEventsToo: true);
        AddHandler(PreviewMouseMoveEvent, new MouseEventHandler(OnPreviewMouseMove), handledEventsToo: true);

        PreviewKeyDown += OnWindowPreviewKeyDown;

        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ShellViewModel old)
            {
                old.MatchShown -= OnMatchShown;
            }

            if (e.NewValue is ShellViewModel shell)
            {
                shell.MatchShown += OnMatchShown;
            }

            WatchSettings();
            WatchArrange();
            WatchDayReport();
            WatchInstructions();
            WatchSetup();
            WatchComposer();
        };
        Activated += (_, _) =>
        {
            RefitPlacement();
            (DataContext as ShellViewModel)?.SyncAccount();
            AllowMotion(true);
        };

        Deactivated += (_, _) => AllowMotion(false);
    }

    private BoardViewModel? Board => (DataContext as ShellViewModel)?.Board;

    /// <summary>F6: the focus to the next pane (G71, B8).</summary>
    public static readonly RoutedCommand NextPaneCommand = new(nameof(NextPaneCommand), typeof(MainWindow));

    /// <summary>Shift+F6: the focus to the previous pane.</summary>
    public static readonly RoutedCommand PreviousPaneCommand = new(nameof(PreviousPaneCommand), typeof(MainWindow));

    /// <summary>Edit › Find cards: the focus to the search box. Its key, Ctrl+F, is handled in OnWindowPreviewKeyDown.</summary>
    public static readonly RoutedCommand FindCardsCommand = new(nameof(FindCardsCommand), typeof(MainWindow));

    // The panes in reading order: the top bar, the sessions panel, the board or the project, the locks and chains panels,
    // and the drawer over them.
    private FrameworkElement[] Panes => [SearchBar, SessionsPane, BoardSurface, ProjectPane, LocksPane, ChainsPane, DrawerPane];

    private void OnNextPane(object sender, ExecutedRoutedEventArgs e) => MovePane(forward: true);

    private void OnPreviousPane(object sender, ExecutedRoutedEventArgs e) => MovePane(forward: false);

    private void OnFindCards(object sender, ExecutedRoutedEventArgs e) => FocusSearch();

    private void MovePane(bool forward)
    {
        FrameworkElement[] panes = Panes;
        int found = Array.FindIndex(panes, pane => ReferenceEquals(Keyboard.FocusedElement, pane)
            || (Keyboard.FocusedElement is DependencyObject focused && pane.IsAncestorOf(focused)));

        // The Tags flyout is a popup, outside the search bar's visual tree, but it is the search bar's pane.
        if (found < 0 && Keyboard.FocusedElement is DependencyObject inFlyout && TagsFlyout.IsAncestorOf(inFlyout))
        {
            found = Array.IndexOf(panes, SearchBar);
        }

        if (PaneCycle.Next([.. panes.Select(pane => pane.IsVisible)], found < 0 ? null : found, forward) is not { } next)
        {
            return;
        }

        FirstFocusable(panes[next])?.Focus();
    }

    /// <summary>
    /// The element itself when it takes the focus, or else its first shown descendant in tree order that does. A pane's
    /// resize edge is passed over: F6 and an opening pane go to its content, and the edge is reached by Tab.
    /// </summary>
    internal static UIElement? FirstFocusable(DependencyObject element)
    {
        if (element is UIElement { Visibility: not Visibility.Visible } or Controls.PaneResizeThumb)
        {
            return null;
        }

        if (element is UIElement { Focusable: true, IsEnabled: true } candidate
            && (candidate is not Control control || control.IsTabStop))
        {
            return candidate;
        }

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            if (FirstFocusable(VisualTreeHelper.GetChild(element, index)) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // Ctrl+F, or / anywhere but a text field, goes to the search box (card-search-and-tags item 1).
    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var slash = e.Key is Key.Oem2 or Key.Divide && Keyboard.Modifiers == ModifierKeys.None
                    && Keyboard.FocusedElement is not System.Windows.Controls.Primitives.TextBoxBase;

        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && IsInChainsPane(Keyboard.FocusedElement as DependencyObject))
        {
            ChainsSearchBox.Focus();
            ChainsSearchBox.SelectAll();
            e.Handled = true;
        }
        else if ((e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) || slash)
        {
            FocusSearch();
            e.Handled = true;
        }
    }

    private void FocusSearch()
    {
        SearchBox.Focus();

        if (SearchBox.Template.FindName("PART_EditableTextBox", SearchBox) is TextBox edit)
        {
            edit.Focus();
            edit.SelectAll();
        }
    }

    // Enter goes to the next match, Ctrl+Enter opens it, Esc clears the search and gives the board the keys back (item 6).
    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ShellViewModel shell || SearchBox.IsDropDownOpen)
        {
            return;
        }

        var command = e.Key switch
        {
            Key.Enter when Keyboard.Modifiers == ModifierKeys.Control => shell.OpenMatchCommand,
            Key.Enter => shell.NextMatchCommand,
            _ => null,
        };

        if (command is not null)
        {
            shell.Board.Search.Remember();

            if (command.CanExecute(null))
            {
                command.Execute(null);
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            shell.Board.Search.Clear();

            if (shell.IsProjectOpen)
            {
                ProjectPane.Focus();
            }
            else
            {
                BoardSurface.Focus();
            }

            e.Handled = true;
        }
    }

    private void OnSearchLostFocus(object sender, KeyboardFocusChangedEventArgs e) => Board?.Search.Remember();

    // Esc in the Tags flyout closes it and gives the focus back to Tags, leaving the ticks as they were; the window's own
    // Esc would clear the card selection instead (A18, G20).
    private void OnNoticeFlyoutKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || NoticeButton.IsChecked != true)
        {
            return;
        }

        NoticeButton.IsChecked = false;
        NoticeButton.Focus();
        e.Handled = true;
    }

    private void OnTagsFlyoutKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        TagsToggle.IsChecked = false;
        TagsToggle.Focus();
        e.Handled = true;
    }

    // Two buttons come from theme templates with no name, so a screen reader announced them as empty buttons (E9, G167):
    // the toolbar's overflow button, held to the row's height of 32 (W56), and the search box's history drop-down.
    private void OnToolBarLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ToolBar toolBar && TemplatePart<System.Windows.Controls.Primitives.ToggleButton>(toolBar) is { } overflow)
        {
            System.Windows.Automation.AutomationProperties.SetName(overflow, "More tools");
            overflow.MaxHeight = 32;
        }

        if (TemplatePart<System.Windows.Controls.Primitives.ToggleButton>(SearchBox) is { } history)
        {
            System.Windows.Automation.AutomationProperties.SetName(history, "Recent searches");
        }
    }

    private static T? TemplatePart<T>(Control owner)
        where T : FrameworkElement
    {
        owner.ApplyTemplate();

        return Descendants(owner).OfType<T>().FirstOrDefault(part => ReferenceEquals(part.TemplatedParent, owner));

        static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);

                yield return child;

                foreach (DependencyObject inner in Descendants(child))
                {
                    yield return inner;
                }
            }
        }
    }

    // Tags › is built as its menu opens, by right-click, the ⋯ button or Shift+F10 alike, from what is selected then (review T2).
    private void OnCardMenuOpened(object sender, RoutedEventArgs e)
    {
        if (Board is not { } board || sender is not FrameworkElement { DataContext: var context })
        {
            return;
        }

        switch (context)
        {
            case WorkstreamCardViewModel card:
                card.TagMenu = board.Tags.MenuFor(card);
                break;
            case ProjectCardViewModel project:
                project.TagMenu = board.Tags.MenuFor(project);
                break;
        }
    }

    // The Tags list is read as it opens, so it counts what is on the cards now (card-search-and-tags item 12).
    private void OnTagsOpened(object sender, RoutedEventArgs e)
    {
        if (Board is { } board)
        {
            board.Search.LoadTagRows(board.Tags.InUse(), board.Tags.ColourOf);
        }
    }

    // A flyout opened from the keyboard takes the focus to its first choice, or a keyboard user cannot enter it (G66).
    private void OnTagsPopupOpened(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () => (TagsAll.IsChecked == true ? TagsAll : TagsAny).Focus());

    // The drawer's first control takes the focus when it opens (W43, G66).
    private void OnDrawerShown(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () => FirstFocusable(DrawerPane)?.Focus());
        }
    }

    private void OnMatchShown(IBoardCard item)
    {
        var row = Board?.Rows.FirstOrDefault(candidate =>
            candidate.Cells.Any(cell => cell.Cards.Contains(item) || cell.Projects.Contains(item)));
        BoardReveal.Show(BoardSurface, item, row);
    }

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

    private void WatchComposer()
    {
        if ((DataContext as ShellViewModel)?.NewWorkstream is not { } composer)
        {
            return;
        }

        composer.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(NewWorkstreamViewModel.IsOpen))
            {
                ShowComposer(composer);
            }
        };
    }

    /// <summary>
    /// Opens New workstream as a dialog over the board, and closes it once the card is created or
    /// Cancel is pressed. Closing it any other way - its close button, Alt+F4 - is Cancel.
    /// </summary>
    private void ShowComposer(NewWorkstreamViewModel composer)
    {
        if (!composer.IsOpen)
        {
            // Cleared here as well as in Closed, so the deferred ShowDialog below sees the window has
            // gone even if it closes before it was ever shown.
            var open = _composer;
            _composer = null;
            open?.Close();

            return;
        }

        if (_composer is not null)
        {
            return;
        }

        var window = new NewWorkstreamWindow { Owner = this, DataContext = DataContext };
        window.Closed += (_, _) =>
        {
            _composer = null;

            if (composer.IsOpen)
            {
                composer.CloseCommand.Execute(null);
            }
        };

        _composer = window;

        // By the time this runs the composer may already be closed, and showing a closed window throws
        // (it took the board down on 2026-11-10). Show it only if it is still the open composer.
        Dispatcher.BeginInvoke(() =>
        {
            if (ReferenceEquals(_composer, window) && composer.IsOpen)
            {
                window.ShowDialog();
            }
        });
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

    private void OnDisplaysChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(RefitPlacement, System.Windows.Threading.DispatcherPriority.Background);

    /// <summary>
    /// Brings the running window back onto a screen when it has been left on none. Windows parks a
    /// window at -32000,-32000 while the displays rearrange, and can leave it there once they wake,
    /// which looks exactly like a hung application (2026-11-02).
    /// </summary>
    private void RefitPlacement()
    {
        var handle = new WindowInteropHelper(this).Handle;

        if (handle == 0
            || OwnWindowPlacement.Capture(handle) is not { } current
            || WindowPlacementFit.Onto(current, Displays.WorkAreas()) is not { } fitted
            || fitted == current)
        {
            return;
        }

        OwnWindowPlacement.Apply(handle, fitted);
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
        _press.Down(e.OriginalSource as DependencyObject, e.GetPosition(this));

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e) => _press.Up();

    /// <summary>Starts a drag of what was pressed, or says why nothing can be dragged here (<see cref="BoardPress"/>).</summary>
    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        var step = _press.Moved(e.GetPosition(this), e.LeftButton == MouseButtonState.Pressed, shell.IsProjectOpen, shell.Board);

        if (step.Refusal is { } refusal)
        {
            shell.DropRefused(refusal);
        }
        else if (step.Drag is { } item)
        {
            DragDrop.DoDragDrop(this, item.DragPayload, DragDropEffects.Move);
        }
    }

    /// <summary>A double-click on a project card opens its view (AC 14).</summary>
    private void OnProjectCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement { DataContext: ProjectCardViewModel project })
        {
            project.OpenCommand?.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>
    /// A click on a project card selects it as a click on a card does (1.1d AC 41), and puts keyboard focus on it so Enter
    /// opens it; a click on one of its buttons only presses the button.
    /// </summary>
    private void OnProjectCardClicked(object sender, MouseButtonEventArgs e)
    {
        // The second click of a double-click that opened the project arrives after the view has replaced the board.
        if (sender is not FrameworkElement { DataContext: ProjectCardViewModel project } face
            || IsWithinAControl(e.OriginalSource as DependencyObject)
            || DataContext is not ShellViewModel { IsProjectOpen: false } shell)
        {
            return;
        }

        var modifiers = Keyboard.Modifiers;

        shell.Board.ClickSelect(project, modifiers.HasFlag(ModifierKeys.Control), modifiers.HasFlag(ModifierKeys.Shift));
        face.Focus();
        e.Handled = true;
    }

    /// <summary>A right-click on a project card outside the selection selects it alone, as on a card.</summary>
    private void OnProjectCardRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ProjectCardViewModel { IsSelected: false } project }
            && DataContext is ShellViewModel shell)
        {
            shell.Board.ClickSelect(project, toggle: false, extend: false);
        }

    }

    /// <summary>Something dropped on a cell's project strip goes to that cell, a card at the top of its cards.</summary>
    private void OnProjectsDrop(object sender, DragEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ColumnViewModel column }
            || Board is not { } board
            || e.Data.GetData(DataFormats.StringFormat) is not string dropped)
        {
            return;
        }

        SayAboutDrop(BoardGesture.Drop(board, dropped, column, 0));
        e.Handled = true;
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

        if (!BoardGesture.StartsBand(source))
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

        Resize(shell, header, panel, e.HorizontalChange);
    }

    /// <summary>The same as dragging the heading's edge, from the border between two cells.</summary>
    private void OnCellEdgeDrag(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ColumnViewModel cell } edge
            || DataContext is not ShellViewModel shell
            || shell.Board.StageHeaders.FirstOrDefault(header => header.Stage == cell.Column) is not { } header
            || FindAncestor<Controls.StageColumnsPanel>(edge) is not { } panel)
        {
            return;
        }

        Resize(shell, header, panel, e.HorizontalChange);
    }

    private static void Resize(ShellViewModel shell, StageHeaderViewModel header, Controls.StageColumnsPanel panel, double pixels)
    {
        var openWidth = panel.ActualWidth - (shell.Board.StageHeaders.Count(other => other.IsCollapsed) * Controls.StageColumnsPanel.FoldedWidth);

        if (openWidth > 0)
        {
            shell.Board.ResizeColumns(header, pixels * shell.Board.OpenColumnWeight / openWidth);
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
            || CellsOf(rowPanel, row) is not { } cells)
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

    /// <summary>The row's own cells, found below the handle's panel.</summary>
    private static ItemsControl? CellsOf(DependencyObject panel, BoardRowViewModel row)
    {
        var pending = new Stack<DependencyObject>([panel]);

        while (pending.TryPop(out var node))
        {
            if (node is ItemsControl { ItemsSource: var source } cells && ReferenceEquals(source, row.Cells))
            {
                return cells;
            }

            for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(node); index++)
            {
                pending.Push(System.Windows.Media.VisualTreeHelper.GetChild(node, index));
            }
        }

        return null;
    }

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

    /// <summary>The cards and project cards on show whose outline the rubber band touches.</summary>
    private IEnumerable<IBoardCard> CardsWithin(Rect area)
    {
        var pending = new Stack<DependencyObject>([BoardSurface]);

        while (pending.TryPop(out var element))
        {
            if (element is FrameworkElement { Tag: "Card" or "ProjectCard", DataContext: IBoardCard card, IsVisible: true } face)
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

    private static bool IsWithinAControl(DependencyObject? source) =>
        BoardGesture.IsWithin<System.Windows.Controls.Primitives.ButtonBase>(source);

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

    private void OnFoldProjectCard(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ProjectCardViewModel project } && DataContext is ShellViewModel shell)
        {
            shell.Board.Fold(project, !project.IsCollapsed);
        }

        e.Handled = true;
    }

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

    /// <summary>
    /// Moves one of the Chains panel's two areas, the tree or what waits above it, by the wheel's own delta, before
    /// anything inside can swallow the event. A precision touchpad's small steps then add up to what a notch would move,
    /// rather than each moving three lines (Nigel, 2026-11-25).
    /// </summary>
    private void OnChainsPanelMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var panel = sender as ScrollViewer ?? (sender as DependencyObject is { } host ? FirstScrollViewerIn(host) : null);

        if (panel is null)
        {
            return;
        }

        e.Handled = true;
        panel.ScrollToVerticalOffset(panel.VerticalOffset - e.Delta);

        // The offset changes only on the next layout, and a touchpad sends several steps before one: without this, each
        // step would start from the same place and all but the last would be lost.
        panel.UpdateLayout();
    }

    private static ScrollViewer? FirstScrollViewerIn(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);

            if ((child as ScrollViewer ?? FirstScrollViewerIn(child)) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private bool IsInChainsPane(DependencyObject? element)
    {
        for (var node = element; node is not null; node = VisualTreeHelper.GetParent(node) ?? LogicalTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, ChainsPane))
            {
                return true;
            }
        }

        return false;
    }

    // Esc clears the chains search, and a second Esc gives the keys to the tree; Down goes to the first row shown
    // (chains-panel-at-scale criterion 6).
    private void OnChainsSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (ChainsSearchBox.Text.Length > 0)
            {
                // At once, not after the binding's typing delay, so the tree is whole again as the box empties.
                ChainsSearchBox.Text = string.Empty;
                ChainsSearchBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }
            else
            {
                FocusFirstChainRow();
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            FocusFirstChainRow();
            e.Handled = true;
        }
    }

    private void FocusFirstChainRow()
    {
        var first = ChainsTree.Items.OfType<ChainNodeViewModel>().FirstOrDefault(node => node.IsShown);

        // The tree builds only the rows in view, so the first row exists only once the tree is back at its top.
        FirstScrollViewerIn(ChainsTree)?.ScrollToTop();
        ChainsTree.UpdateLayout();

        if (first is not null && ChainsTree.ItemContainerGenerator.ContainerFromItem(first) is TreeViewItem row)
        {
            row.Focus();
        }
        else
        {
            ChainsTree.Focus();
        }
    }

    // Alt+Down opens the Filter menu, as it opens any drop-down (criterion 20).
    private void OnChainsFilterKeyDown(object sender, KeyEventArgs e)
    {
        if (e.SystemKey == Key.Down && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            OnOverflowClick(sender, e);
            e.Handled = true;
        }
    }

    // The counts line is a live region: a screen reader hears what the filters now hide (criterion 21).
    private void OnChainsSummaryChanged(object? sender, System.Windows.Data.DataTransferEventArgs e)
    {
        if (sender is TextBlock line && System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(line) is { } peer)
        {
            peer.RaiseAutomationEvent(System.Windows.Automation.Peers.AutomationEvents.LiveRegionChanged);
        }
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

        if (Board is not { } board)
        {
            return;
        }

        // The cell carries both axes, so a drop says stage and area at once. Dragging within a row
        // is the same gesture as dragging across one; only the cell landed in is different.
        SayAboutDrop(BoardGesture.Drop(board, key, column, DropIndex(lane, column, e.GetPosition(lane))));
        e.Handled = true;
    }

    /// <summary>Puts what a drop has to say in the status bar (1.1e AC 3).</summary>
    private void SayAboutDrop(string? notice)
    {
        if (notice is not null && DataContext is ShellViewModel shell)
        {
            shell.DropRefused(notice);
        }
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

    private static DependencyObject? ParentOf(DependencyObject source) => BoardGesture.ParentOf(source);
}
