using System.Windows;
using System.Windows.Media;
using Theseus.App.ViewModels;

namespace Theseus.App.Views;

/// <summary>
/// One connector of the project view as drawn (1.1d AC 25): its line, as points in order, and its filled arrowhead,
/// whose tip is where the line ends.
/// </summary>
/// <param name="Kind">Which link it draws.</param>
/// <param name="Line">The line's points, from its start to the base of its arrowhead.</param>
/// <param name="Arrow">The arrowhead's three corners, its tip first.</param>
public sealed record ConnectorFigure(ConnectorKind Kind, IReadOnlyList<Point> Line, IReadOnlyList<Point> Arrow)
{
    /// <summary>Where the connector ends: its arrowhead's tip.</summary>
    public Point Tip => Arrow[0];

    /// <summary>The line as a geometry, open and unfilled.</summary>
    public Geometry LineGeometry() => Figure(Line, closed: false);

    /// <summary>The arrowhead as a geometry, closed and filled.</summary>
    public Geometry ArrowGeometry() => Figure(Arrow, closed: true);

    private static PathGeometry Figure(IReadOnlyList<Point> points, bool closed)
    {
        var figure = new PathFigure { StartPoint = points[0], IsClosed = closed, IsFilled = closed };

        foreach (var point in points.Skip(1))
        {
            figure.Segments.Add(new LineSegment(point, true));
        }

        var geometry = new PathGeometry([figure]);
        geometry.Freeze();

        return geometry;
    }
}
