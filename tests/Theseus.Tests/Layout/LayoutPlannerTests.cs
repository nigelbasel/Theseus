using Theseus.Core.Layout;

namespace Theseus.Tests.Layout;

[TestFixture]
public class LayoutPlannerTests
{
    // Nigel's display: 3456x2160 physical, less a 72px taskbar.
    private static readonly LayoutRect Area = new(0, 0, 3456, 2088);

    [Test]
    public void Tile_PutsTwoWindowsSideBySideRatherThanOneAboveTheOther()
    {
        var placed = LayoutPlanner.Tile(Area, 2);

        Assert.Multiple(() =>
        {
            Assert.That(placed[0], Is.EqualTo(new LayoutRect(0, 0, 1728, 2088)));
            Assert.That(placed[1], Is.EqualTo(new LayoutRect(1728, 0, 1728, 2088)));
        });
    }

    [Test]
    public void Tile_PutsThreeWindowsInATwoByTwoRatherThanThreeAcross()
    {
        var placed = LayoutPlanner.Tile(Area, 3);

        Assert.That(placed, Has.Count.EqualTo(3));
        Assert.That(placed[0].Width, Is.EqualTo(1728),
            "three across would be 1152 wide, which is a third narrower for no gain in height");
        Assert.That(placed[2].Y, Is.EqualTo(1044), "the third drops to a second row");
    }

    [Test]
    public void Tile_LeavesNoStripOfDesktopShowingWhenTheWidthDoesNotDivide()
    {
        var placed = LayoutPlanner.Tile(new LayoutRect(0, 0, 1001, 1000), 2);

        Assert.That(placed[0].Width + placed[1].Width, Is.EqualTo(1001),
            "the last window in a row takes the remainder");
    }

    [Test]
    public void Tile_StartsFromTheAreaRatherThanTheOrigin()
    {
        var placed = LayoutPlanner.Tile(new LayoutRect(100, 50, 800, 600), 2);

        Assert.That(placed[0].X, Is.EqualTo(100));
        Assert.That(placed[0].Y, Is.EqualTo(50));
    }

    [Test]
    public void Tile_LeavesAGapWhenAskedTo()
    {
        var placed = LayoutPlanner.Tile(Area, 2, gap: 8);

        Assert.That(placed[0], Is.EqualTo(new LayoutRect(8, 8, 1712, 2072)));
    }

    [Test]
    public void Tile_GivesOneWindowTheWholeArea()
    {
        Assert.That(LayoutPlanner.Tile(Area, 1), Is.EqualTo(new[] { Area }));
    }

    [Test]
    public void Tile_PlacesNothingForNothing()
    {
        Assert.That(LayoutPlanner.Tile(Area, 0), Is.Empty);
    }

    [Test]
    public void Cascade_OffsetsEachWindowAndKeepsTheLastOneInside()
    {
        var placed = LayoutPlanner.Cascade(Area, 4, step: 50);

        Assert.Multiple(() =>
        {
            Assert.That(placed[0].X, Is.EqualTo(0));
            Assert.That(placed[3].X, Is.EqualTo(150));
            Assert.That(placed[3].X + placed[3].Width, Is.EqualTo(Area.Width),
                "the last window's right edge lands exactly on the edge of the area");
            Assert.That(placed[3].Y + placed[3].Height, Is.EqualTo(Area.Height));
        });
    }

    [Test]
    public void Cascade_KeepsEveryWindowTheSameSize()
    {
        var placed = LayoutPlanner.Cascade(Area, 5);

        var sizes = placed.Select(rect => (rect.Width, rect.Height)).Distinct().ToList();

        Assert.That(sizes, Has.Count.EqualTo(1));
    }

    [Test]
    public void Stack_GivesEveryWindowTheWholeArea()
    {
        Assert.That(LayoutPlanner.Stack(Area, 3), Is.EqualTo(new[] { Area, Area, Area }));
    }

    [Test]
    public void Plan_DispatchesOnTheKindItIsGiven()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LayoutPlanner.Plan(ArrangementKind.Tile, Area, 2)[1].X, Is.EqualTo(1728));
            Assert.That(LayoutPlanner.Plan(ArrangementKind.Cascade, Area, 2)[1].X, Is.EqualTo(48));
            Assert.That(LayoutPlanner.Plan(ArrangementKind.Stack, Area, 2)[1], Is.EqualTo(Area));
        });
    }
}
