using Theseus.Core.Layout;

namespace Theseus.Tests.Layout;

[TestFixture]
public class TileShapeTests
{
    private static readonly LayoutRect Wide = new(0, 0, 3456, 2088);
    private static readonly LayoutRect Tall = new(0, 0, 1080, 1920);

    [TestCase(1, 1, 1)]
    [TestCase(2, 2, 1)]
    [TestCase(3, 2, 2)]
    [TestCase(4, 2, 2)]
    [TestCase(6, 3, 2)]
    [TestCase(9, 3, 3)]
    public void For_ChoosesAGridThatKeepsTilesTheShapeOfTheScreen(int count, int columns, int rows)
    {
        Assert.That(TileShape.For(count, Wide), Is.EqualTo((columns, rows)));
    }

    [Test]
    public void For_FavoursRowsOnATallScreenWhereItFavouredColumnsOnAWideOne()
    {
        Assert.That(TileShape.For(2, Tall), Is.EqualTo((1, 2)),
            "two windows on a portrait display belong one above the other");
    }

    [Test]
    public void For_NeverAsksForMoreTilesThanThereAreWindows()
    {
        for (var count = 1; count <= 12; count++)
        {
            var (columns, rows) = TileShape.For(count, Wide);

            Assert.That(columns * rows, Is.GreaterThanOrEqualTo(count), $"{count} windows must all fit");
            Assert.That((columns - 1) * rows, Is.LessThan(count),
                $"{count} windows in {columns}x{rows} leaves a whole column empty");
        }
    }

    [Test]
    public void For_StillReturnsAUsableGridForAnAreaOfNoSize()
    {
        var (columns, rows) = TileShape.For(3, new LayoutRect(0, 0, 0, 0));

        Assert.That(columns * rows, Is.GreaterThanOrEqualTo(3),
            "a monitor that reports nothing should not also produce a grid that holds nothing");
    }
}
