using Theseus.Core.Layout;

namespace Theseus.Tests.Layout;

[TestFixture]
public class ReadableWidthTests
{
    [Test]
    public void Columns_MeasuresAFullWidthWindowOnNigelsDisplay()
    {
        Assert.That(ReadableWidth.Columns(3456), Is.EqualTo(235).Within(10));
    }

    [Test]
    public void Of_CallsHalfOfThatDisplayComfortable()
    {
        Assert.That(ReadableWidth.Of(1728), Is.EqualTo(WidthVerdict.Comfortable),
            "two windows side by side is the arrangement this feature exists for");
    }

    [Test]
    public void Of_CallsAThirdCrampedAndAQuarterUnusable()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReadableWidth.Of(1152), Is.EqualTo(WidthVerdict.Cramped),
                "three across comes to 75 columns once chrome is taken off, not the 80 first guessed");
            Assert.That(ReadableWidth.Of(864), Is.EqualTo(WidthVerdict.Unusable));
        });
    }

    [Test]
    public void Of_RefusesToCallSomethingUnreadableMerelySmall()
    {
        Assert.That(ReadableWidth.Of(480), Is.EqualTo(WidthVerdict.Unusable),
            "an arrangement this narrow is not a worse version of a good one, it is no use at all");
    }

    [Test]
    public void Columns_CopesWithAWindowNarrowerThanItsOwnChrome()
    {
        Assert.That(ReadableWidth.Columns(20), Is.Zero);
    }

    [Test]
    public void Columns_CopesWithAFontOfNoWidth()
    {
        Assert.That(ReadableWidth.Columns(1000, advance: 0), Is.Zero);
    }

    [Test]
    public void Describe_SaysBothTheNumberAndWhatToMakeOfIt()
    {
        var said = ReadableWidth.Describe(1728);

        Assert.That(said, Does.Contain("columns"));
        Assert.That(said, Does.Contain("comfortable"));
    }

    [Test]
    public void Judge_TreatsTheEightyColumnBoundaryAsTheOneThatMatters()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ReadableWidth.Judge(80), Is.EqualTo(WidthVerdict.Usable));
            Assert.That(ReadableWidth.Judge(79), Is.EqualTo(WidthVerdict.Cramped));
        });
    }
}
