using Theseus.Core.Colour;

namespace Theseus.Tests.Colour;

[TestFixture]
public class TabColourAllocatorTests
{
    private TabColourAllocator _allocator = null!;

    [SetUp]
    public void SetUp() => _allocator = new TabColourAllocator();

    private static ColourAllocationState InUse(params string[] colours) =>
        new(colours, []);

    [Test]
    public void Palette_HasSixteenDistinctEntries()
    {
        Assert.That(TabColourPalette.All, Has.Count.EqualTo(16));
        Assert.That(TabColourPalette.All.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(16));
    }

    [Test]
    public void Allocate_WhenNothingIsInUse_ReturnsTheFirstPaletteEntry()
    {
        var result = _allocator.Allocate(ColourAllocationState.Empty);

        Assert.That(result, Is.EqualTo(TabColourPalette.All[0]));
    }

    [Test]
    public void Allocate_NeverReturnsAColourAlreadyInUse()
    {
        var used = new List<string>();

        for (var n = 0; n < TabColourPalette.All.Count; n++)
        {
            var next = _allocator.Allocate(InUse([.. used]));

            Assert.That(used, Does.Not.Contain(next), $"allocation {n} reused {next}");
            used.Add(next);
        }
    }

    [Test]
    public void Allocate_AcrossTheWholePalette_YieldsEveryColourExactlyOnce()
    {
        var used = new List<string>();

        for (var n = 0; n < TabColourPalette.All.Count; n++)
        {
            used.Add(_allocator.Allocate(InUse([.. used])));
        }

        Assert.That(used.Order(), Is.EqualTo(TabColourPalette.All.Order()).AsCollection);
    }

    [Test]
    public void Allocate_WhenOneColourIsInUse_ReturnsThePerceptuallyFarthestEntry()
    {
        var first = TabColourPalette.All[0];

        var result = _allocator.Allocate(InUse(first));

        var chosen = OkLabConverter.FromHex(result);
        var anchor = OkLabConverter.FromHex(first);
        var chosenDistance = OkLabConverter.Distance(anchor, chosen);

        foreach (var candidate in TabColourPalette.All.Where(c => c != first))
        {
            var distance = OkLabConverter.Distance(anchor, OkLabConverter.FromHex(candidate));
            Assert.That(chosenDistance, Is.GreaterThanOrEqualTo(distance - 1e-12),
                $"{candidate} is farther from {first} than the chosen {result}");
        }
    }

    [Test]
    public void Allocate_IsDeterministicForTheSameState()
    {
        var state = InUse(TabColourPalette.All[3], TabColourPalette.All[9]);

        var a = _allocator.Allocate(state);
        var b = _allocator.Allocate(state);

        Assert.That(b, Is.EqualTo(a));
    }

    [Test]
    public void Allocate_BreaksTiesOnTheLowerPaletteIndex()
    {
        var state = InUse(TabColourPalette.All[0]);

        var result = _allocator.Allocate(state);

        var anchor = OkLabConverter.FromHex(TabColourPalette.All[0]);
        var winning = OkLabConverter.Distance(anchor, OkLabConverter.FromHex(result));

        var tied = TabColourPalette.All
            .Where(c => c != TabColourPalette.All[0])
            .Where(c => Math.Abs(OkLabConverter.Distance(anchor, OkLabConverter.FromHex(c)) - winning) < 1e-9)
            .ToList();

        var lowestTied = tied.OrderBy(TabColourPalette.IndexOf).First();
        Assert.That(result, Is.EqualTo(lowestTied));
    }

    [Test]
    public void Allocate_TakesAccountOfManualColoursOutsideThePalette()
    {
        var offPalette = "#FF0000";

        var result = _allocator.Allocate(InUse(offPalette));

        Assert.That(TabColourPalette.All, Does.Contain(result));

        var anchor = OkLabConverter.FromHex(offPalette);
        var chosenDistance = OkLabConverter.Distance(anchor, OkLabConverter.FromHex(result));
        var nearestPossible = TabColourPalette.All
            .Min(c => OkLabConverter.Distance(anchor, OkLabConverter.FromHex(c)));

        Assert.That(chosenDistance, Is.GreaterThan(nearestPossible));
    }

    [Test]
    public void Allocate_WhenEveryColourIsInUse_ReturnsTheOldestFreedColour()
    {
        var freed = new[]
        {
            new TabColourRelease(TabColourPalette.All[7], new DateTimeOffset(2026, 8, 20, 9, 0, 0, TimeSpan.Zero)),
            new TabColourRelease(TabColourPalette.All[2], new DateTimeOffset(2026, 8, 11, 9, 0, 0, TimeSpan.Zero)),
            new TabColourRelease(TabColourPalette.All[5], new DateTimeOffset(2026, 8, 25, 9, 0, 0, TimeSpan.Zero)),
        };
        var state = new ColourAllocationState([.. TabColourPalette.All], freed);

        var result = _allocator.Allocate(state);

        Assert.That(result, Is.EqualTo(TabColourPalette.All[2]));
    }

    [Test]
    public void Allocate_WhenEveryColourIsInUseAndNoneWereFreed_ReturnsTheLeastUsedLowestIndex()
    {
        var inUse = new List<string>(TabColourPalette.All);
        inUse.Add(TabColourPalette.All[0]);
        inUse.Add(TabColourPalette.All[1]);

        var result = _allocator.Allocate(new ColourAllocationState(inUse, []));

        Assert.That(result, Is.EqualTo(TabColourPalette.All[2]));
    }

    [Test]
    public void Allocate_IgnoresFreedColoursThatAreNotInThePalette()
    {
        var freed = new[]
        {
            new TabColourRelease("#123456", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            new TabColourRelease(TabColourPalette.All[4], new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)),
        };
        var state = new ColourAllocationState([.. TabColourPalette.All], freed);

        var result = _allocator.Allocate(state);

        Assert.That(result, Is.EqualTo(TabColourPalette.All[4]));
    }
}
