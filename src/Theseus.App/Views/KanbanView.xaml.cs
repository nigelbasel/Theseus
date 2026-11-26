using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Theseus.App.ViewModels;

namespace Theseus.App.Views;

/// <summary>
/// The projects kanban's view (projects-kanban spec AC 11 to 15, 23). It holds the headers still against the body's
/// scrolling, turns edge drags and double-clicks into the column's resize and fit, measures cards for a fit, and moves
/// focus between cards with the arrow keys. What anything means is the view model's.
/// </summary>
public partial class KanbanView : UserControl
{
    /// <summary>How far Ctrl+Left and Ctrl+Right resize a focused column (AC 23).</summary>
    public const double KeyResizeStep = 16;

    /// <summary>Creates the view.</summary>
    public KanbanView() => InitializeComponent();

    /// <summary>
    /// Moves every row header right by the body's horizontal offset, so the headers stay at the left while the cells
    /// scroll under them (AC 5, 15). One transform, shared by every header.
    /// </summary>
    public TranslateTransform RowHeaderShift { get; } = new();

    private KanbanViewModel? Kanban => DataContext as KanbanViewModel;

    private void OnBodyScrolled(object sender, ScrollChangedEventArgs e)
    {
        RowHeaderShift.X = Body.HorizontalOffset;
        HeaderShift.X = -Body.HorizontalOffset;
    }

    // Shift and the wheel scroll sideways, as Windows lists do (AC 15).
    private void OnBodyWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            Body.ScrollToHorizontalOffset(Body.HorizontalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private void OnEdgeDrag(object sender, DragDeltaEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is KanbanColumnViewModel column)
        {
            column.Resize(column.OpenWidth + e.HorizontalChange);
        }
    }

    private void OnEdgeDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is KanbanColumnViewModel column)
        {
            column.Fit(WidestCard(column));
            e.Handled = true;
        }
    }

    private void OnFitAll(object sender, ExecutedRoutedEventArgs e) => Kanban?.FitAll(WidestCard);

    /// <summary>
    /// The widest of a column's cards at their natural width, with the cell's padding: what double-clicking its edge
    /// fits it to (AC 13). Zero when it holds no card drawn.
    /// </summary>
    internal double WidestCard(KanbanColumnViewModel column)
    {
        var widest = 0.0;

        foreach (var card in Descendants<Border>(RowList).Where(border => Equals(border.Tag, "Card")
                     && border.DataContext is KanbanCardViewModel each && each.Column == column.Column))
        {
            card.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            widest = Math.Max(widest, card.DesiredSize.Width);
            card.InvalidateMeasure();
        }

        // The cell's padding (8 each side) and its 1 px border.
        return widest > 0 ? widest + 17 : 0;
    }

    private void OnColumnKeyDown(object sender, KeyEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not KanbanColumnViewModel column || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            return;
        }

        if (e.Key == Key.Left)
        {
            column.ResizeBy(-KeyResizeStep);
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            column.ResizeBy(KeyResizeStep);
            e.Handled = true;
        }
    }

    private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { DataContext: KanbanCardViewModel card } frame)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            card.OpenCommand.Execute(null);
            e.Handled = true;
        }
        else
        {
            frame.Focus();
        }
    }

    // The menu is made when it is asked for, by right-click, Shift+F10 or the menu key, not once per card drawn: a
    // hundred cards each carrying a built menu was most of the kanban's layout time.
    private void OnCardMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not Border { DataContext: KanbanCardViewModel card } frame)
        {
            return;
        }

        e.Handled = true;

        var menu = new ContextMenu
        {
            ItemsSource = card.MenuEntries,
            ItemContainerStyle = (Style)FindResource("KanbanMenuItem"),
            PlacementTarget = frame,
            Placement = e.CursorLeft < 0 ? PlacementMode.Bottom : PlacementMode.MousePoint,
        };

        menu.IsOpen = true;
    }

    // Left and Right go to the neighbouring cell's first card in the row, Up and Down within the cell (AC 23).
    private void OnCardKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not Border { DataContext: KanbanCardViewModel card } || Keyboard.Modifiers != ModifierKeys.None
            || Kanban?.Rows.FirstOrDefault(row => row.Cards.Contains(card)) is not { } row)
        {
            return;
        }

        var cell = row.Cells.First(each => each.Cards.Contains(card));
        var at = cell.Cards.ToList().IndexOf(card);
        var cellAt = row.Cells.ToList().IndexOf(cell);

        KanbanCardViewModel? target = e.Key switch
        {
            Key.Down => cell.Cards.ElementAtOrDefault(at + 1),
            Key.Up => at > 0 ? cell.Cards[at - 1] : null,
            Key.Right => row.Cells.Skip(cellAt + 1).FirstOrDefault(each => each.Column.IsOpen && each.Cards.Count > 0)?.Cards[0],
            Key.Left => row.Cells.Take(cellAt).LastOrDefault(each => each.Column.IsOpen && each.Cards.Count > 0)?.Cards[0],
            _ => null,
        };

        if (target is not null && Descendants<Border>(RowList).FirstOrDefault(border => Equals(border.Tag, "Card") && border.DataContext == target) is { } frame)
        {
            frame.Focus();
            frame.BringIntoView();
            e.Handled = true;
        }
    }

    // One row opens it; several open a menu of them (AC 17).
    private void OnLinkClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: IReadOnlyList<CardItemRow> rows } button || rows.Count == 0)
        {
            return;
        }

        if (rows.Count == 1)
        {
            rows[0].OpenCommand.Execute(null);

            return;
        }

        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };

        foreach (var row in rows)
        {
            menu.Items.Add(new MenuItem { Header = row.Display, Command = row.OpenCommand, ToolTip = row.Tooltip });
        }

        menu.IsOpen = true;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (var each in Descendants<T>(child))
            {
                yield return each;
            }
        }
    }
}
