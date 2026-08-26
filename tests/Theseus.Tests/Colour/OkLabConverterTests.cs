using Theseus.Core.Colour;

namespace Theseus.Tests.Colour;

[TestFixture]
public class OkLabConverterTests
{
    [Test]
    public void FromHex_Black_HasZeroLightness()
    {
        var c = OkLabConverter.FromHex("#000000");

        Assert.That(c.L, Is.EqualTo(0d).Within(0.001));
    }

    [Test]
    public void FromHex_White_HasUnitLightness()
    {
        var c = OkLabConverter.FromHex("#FFFFFF");

        Assert.That(c.L, Is.EqualTo(1d).Within(0.001));
    }

    [Test]
    public void FromHex_IsCaseInsensitiveAndHashOptional()
    {
        var a = OkLabConverter.FromHex("#208DD7");
        var b = OkLabConverter.FromHex("208dd7");

        Assert.That(b.L, Is.EqualTo(a.L).Within(1e-12));
        Assert.That(b.A, Is.EqualTo(a.A).Within(1e-12));
        Assert.That(b.B, Is.EqualTo(a.B).Within(1e-12));
    }

    [TestCase("")]
    [TestCase("#12345")]
    [TestCase("#GGGGGG")]
    [TestCase("not a colour")]
    public void FromHex_RejectsMalformedInput(string value)
    {
        Assert.That(() => OkLabConverter.FromHex(value), Throws.TypeOf<FormatException>());
    }

    [Test]
    public void EveryPaletteEntry_SitsOnTheConstantLightnessBand()
    {
        foreach (var hex in TabColourPalette.All)
        {
            var c = OkLabConverter.FromHex(hex);

            Assert.That(c.L, Is.EqualTo(TabColourPalette.Lightness).Within(0.005),
                $"{hex} is off the band at L={c.L:F4}");
        }
    }

    [Test]
    public void Distance_FromAColourToItself_IsZero()
    {
        var c = OkLabConverter.FromHex("#0A9C85");

        Assert.That(OkLabConverter.Distance(c, c), Is.EqualTo(0d).Within(1e-12));
    }

    [Test]
    public void Distance_IsSymmetric()
    {
        var a = OkLabConverter.FromHex("#CF5D61");
        var b = OkLabConverter.FromHex("#208DD7");

        Assert.That(OkLabConverter.Distance(a, b), Is.EqualTo(OkLabConverter.Distance(b, a)).Within(1e-12));
    }

    [Test]
    public void Distance_BetweenOpposingHues_ExceedsDistanceBetweenNeighbours()
    {
        var red = OkLabConverter.FromHex(TabColourPalette.All[0]);
        var neighbour = OkLabConverter.FromHex(TabColourPalette.All[1]);
        var opposite = OkLabConverter.FromHex(TabColourPalette.All[8]);

        Assert.That(OkLabConverter.Distance(red, opposite),
            Is.GreaterThan(OkLabConverter.Distance(red, neighbour)));
    }
}
