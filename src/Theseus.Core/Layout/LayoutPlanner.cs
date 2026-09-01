namespace Theseus.Core.Layout;

/// <summary>
/// Where each window goes. Pure arithmetic over an area and a count, so the part most likely to be
/// wrong is the part that can be tested without a desktop.
/// </summary>
public static class LayoutPlanner
{
    /// <summary>The rectangles for one arrangement, in the order the windows were given.</summary>
    /// <param name="kind">Which arrangement.</param>
    /// <param name="area">The area to place them in, usually a monitor's work area.</param>
    /// <param name="count">How many windows.</param>
    /// <param name="gap">Space left between tiles, and around them. Ignored by the other kinds.</param>
    /// <param name="step">How far each cascaded window is offset from the one before.</param>
    public static IReadOnlyList<LayoutRect> Plan(
        ArrangementKind kind, LayoutRect area, int count, int gap = 0, int step = 48) => kind switch
    {
        ArrangementKind.Tile => Tile(area, count, gap),
        ArrangementKind.Cascade => Cascade(area, count, step),
        _ => Stack(area, count),
    };

    /// <summary>A grid filling the area, with nothing overlapping.</summary>
    public static IReadOnlyList<LayoutRect> Tile(LayoutRect area, int count, int gap = 0)
    {
        if (count <= 0)
        {
            return [];
        }

        var (columns, rows) = TileShape.For(count, area);
        var width = area.Width / columns;
        var height = area.Height / rows;
        var placed = new List<LayoutRect>(count);

        for (var index = 0; index < count; index++)
        {
            var column = index % columns;
            var row = index / columns;

            // The last window in a row takes whatever is left over, so a width that does not divide
            // evenly leaves no strip of desktop showing down the right-hand side.
            var isLastColumn = column == columns - 1 || index == count - 1;
            var isLastRow = row == rows - 1;

            var cell = new LayoutRect(
                area.X + (column * width),
                area.Y + (row * height),
                isLastColumn ? area.Width - (column * width) : width,
                isLastRow ? area.Height - (row * height) : height);

            placed.Add(gap > 0 ? cell.Inset(gap) : cell);
        }

        return placed;
    }

    /// <summary>
    /// Overlapping, each offset from the one before, all the same size and all fitting. The size
    /// shrinks as the count grows, because the last window must still be inside the area - which is
    /// what stops a cascade of nine walking off the bottom of the screen.
    /// </summary>
    public static IReadOnlyList<LayoutRect> Cascade(LayoutRect area, int count, int step = 48)
    {
        if (count <= 0)
        {
            return [];
        }

        var travel = step * (count - 1);
        var width = Math.Max(1, area.Width - travel);
        var height = Math.Max(1, area.Height - travel);

        return [.. Enumerable.Range(0, count).Select(index =>
            new LayoutRect(area.X + (index * step), area.Y + (index * step), width, height))];
    }

    /// <summary>Every window the whole area, one on top of another.</summary>
    public static IReadOnlyList<LayoutRect> Stack(LayoutRect area, int count) =>
        count <= 0 ? [] : [.. Enumerable.Repeat(area, count)];
}
