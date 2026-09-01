namespace Theseus.Core.Layout;

/// <summary>
/// How many columns and rows to break an area into for a given number of windows.
///
/// The obvious rule - a square-ish grid - is wrong on a wide screen, where it makes tiles far taller
/// than they are wide. Terminals want the opposite: width is what limits how much of a line is
/// readable, and height beyond forty rows or so is worth little. So the grid is chosen to make each
/// tile as close as possible in shape to the area being divided, which on a wide display means
/// favouring columns and on a tall one means favouring rows.
/// </summary>
public static class TileShape
{
    /// <summary>The grid to use, as columns by rows.</summary>
    /// <param name="count">How many windows are being placed.</param>
    /// <param name="area">The area they are being placed in.</param>
    public static (int Columns, int Rows) For(int count, LayoutRect area)
    {
        if (count <= 1)
        {
            return (1, 1);
        }

        // A square-ish grid, rounded up on the columns, which is what makes two go side by side
        // rather than one above the other. Then turned on its side for a portrait display, where the
        // same reasoning points the other way.
        //
        // Scoring candidate grids by how close each tile's shape came to the screen's was tried and
        // abandoned: for two windows on a 1.65 screen, stacked and side-by-side are exactly equally
        // far from it, and the tie fell to whichever was tested first. The symmetry was the flaw -
        // too narrow and too short are not equally bad when the content is a terminal.
        var columns = (int)Math.Ceiling(Math.Sqrt(count));
        var rows = (int)Math.Ceiling(count / (double)columns);

        return area.Height > area.Width ? (rows, columns) : (columns, rows);
    }
}
