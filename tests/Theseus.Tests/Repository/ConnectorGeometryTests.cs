using System.Windows;
using Theseus.App.ViewModels;
using Theseus.App.Views;

namespace Theseus.Tests.Repository;

/// <summary>Where the project view's connectors run (1.1d AC 25), from the boxes' rectangles.</summary>
[TestFixture]
public class ConnectorGeometryTests
{
    private static readonly Rect Parent = new(0, 0, 290, 100);

    private static readonly Rect Child = new(53, 114, 290, 100);

    [Test]
    public void AHandOver_DropsFromTheParentThenTurnsIntoTheChildsLeftEdge()
    {
        var figure = ConnectorGeometry.HandsOver(Parent, Child);

        Assert.That(figure.Kind, Is.EqualTo(ConnectorKind.HandsOver));
        Assert.That(figure.Line, Is.EqualTo(new[] { new Point(18, 100), new Point(18, 130), new Point(46, 130) }));
        Assert.That(figure.Tip, Is.EqualTo(new Point(53, 130)), "the arrow ends on the child's left edge, 16 px down");
    }

    [Test]
    public void ThenWorkedOn_RunsStraightDown_30PxIn_ToTheNextBoxsTop()
    {
        var figure = ConnectorGeometry.ThenWorkedOn(Parent, new Rect(0, 114, 290, 100));

        Assert.That(figure.Line, Is.EqualTo(new[] { new Point(30, 100), new Point(30, 107) }));
        Assert.That(figure.Tip, Is.EqualTo(new Point(30, 114)));
    }

    [Test]
    public void TheArrowhead_IsA7PxTriangle_PointingAlongTheLastSegment()
    {
        var right = ConnectorGeometry.HandsOver(Parent, Child).Arrow;
        var down = ConnectorGeometry.ThenWorkedOn(Parent, Child).Arrow;

        Assert.That(right, Is.EqualTo(new[] { new Point(53, 130), new Point(46, 126.5), new Point(46, 133.5) }));
        Assert.That(down, Is.EqualTo(new[] { new Point(30, 114), new Point(26.5, 107), new Point(33.5, 107) }));
        Assert.That(ConnectorGeometry.HandsOver(Parent, Child).ArrowGeometry().Bounds, Is.EqualTo(new Rect(46, 126.5, 7, 7)));
    }
}
