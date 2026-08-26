using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>
/// The one window. Its code-behind holds only what genuinely belongs to the visual tree: drag and
/// drop, which is a gesture, not a decision. Every decision lives one project down.
/// </summary>
public partial class MainWindow : Window
{
    private Point? _dragOrigin;
    private SettingsWindow? _settings;

    /// <summary>Creates the window.</summary>
    public MainWindow()
    {
        InitializeComponent();

        FitToWorkArea();

        AddHandler(PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnPreviewMouseDown), handledEventsToo: true);
        AddHandler(PreviewMouseLeftButtonUpEvent, new MouseButtonEventHandler(OnPreviewMouseUp), handledEventsToo: true);
        AddHandler(PreviewMouseMoveEvent, new MouseEventHandler(OnPreviewMouseMove), handledEventsToo: true);

        DataContextChanged += (_, _) => WatchSettings();
    }

    private BoardViewModel? Board => (DataContext as ShellViewModel)?.Board;

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

    /// <summary>
    /// Sizes the window to what the display can actually show. A fixed default is wrong on a scaled
    /// screen: the right-hand panel and the header controls end up beyond the edge of the desktop.
    /// </summary>
    private void FitToWorkArea()
    {
        var area = SystemParameters.WorkArea;

        Width = Math.Min(Width, area.Width - 40);
        Height = Math.Min(Height, area.Height - 40);
        Left = area.Left + ((area.Width - Width) / 2);
        Top = area.Top + ((area.Height - Height) / 2);
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
        if (sender is not ItemsControl { DataContext: ColumnViewModel column }
            || e.Data.GetData(DataFormats.StringFormat) is not string key)
        {
            return;
        }

        Board?.Move(key, column.Column);
        e.Handled = true;
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
