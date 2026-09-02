using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
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
    private HelpWindow? _help;
    private ArrangeWindow? _arrange;

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
            WatchHelp();
            WatchArrange();
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

    private void WatchHelp()
    {
        if ((DataContext as ShellViewModel)?.Help is not { } help)
        {
            return;
        }

        help.PropertyChanged += (_, changed) =>
        {
            if (changed.PropertyName == nameof(HelpViewModel.IsOpen))
            {
                ShowHelp(help);
            }
        };
    }

    /// <summary>
    /// Shows the help beside the board rather than over it, and without holding it. Somebody reads
    /// this while pressing the button it describes, so it must not be a dialog.
    /// </summary>
    private void ShowHelp(HelpViewModel help)
    {
        if (_help is not null)
        {
            if (help.IsOpen)
            {
                _help.Activate();
            }
            else
            {
                _help.Close();
            }

            return;
        }

        if (!help.IsOpen)
        {
            return;
        }

        _help = new HelpWindow { Owner = this, DataContext = help };
        _help.Closed += (_, _) =>
        {
            _help = null;
            help.IsOpen = false;
        };

        _help.Show();
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
            || IsWithinAControl(e.OriginalSource as DependencyObject))
        {
            return;
        }

        (DataContext as ShellViewModel)?.SelectCardCommand.Execute(card);
    }

    private static bool IsWithinAControl(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void OnColumnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.StringFormat)
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

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}
