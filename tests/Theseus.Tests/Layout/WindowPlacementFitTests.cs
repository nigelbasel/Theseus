using Theseus.Core.Layout;

namespace Theseus.Tests.Layout;

[TestFixture]
public class WindowPlacementFitTests
{
    private static readonly LayoutRect Laptop = new(0, 0, 3456, 2088);
    private static readonly LayoutRect Second = new(3456, 0, 1920, 1080);

    [Test]
    public void Onto_KeepsAPositionThatIsStillOnAScreen()
    {
        var saved = Placement(200, 100, 1400, 900);

        Assert.That(WindowPlacementFit.Onto(saved, [Laptop]), Is.EqualTo(saved));
    }

    [Test]
    public void Onto_RescuesAWindowLeftOnAMonitorThatIsNoLongerThere()
    {
        var saved = Placement(4000, 200, 1400, 900);

        var fitted = WindowPlacementFit.Onto(saved, [Laptop]);

        Assert.That(fitted, Is.Not.Null);
        Assert.That(WindowPlacementFit.IsReachable(fitted!.Bounds, [Laptop]), Is.True,
            "undocking must not leave the window somewhere nobody can see or reach");
    }

    [Test]
    public void Onto_KeepsAWindowOnTheSecondScreenWhileItIsStillAttached()
    {
        var saved = Placement(3600, 100, 1400, 800);

        Assert.That(WindowPlacementFit.Onto(saved, [Laptop, Second]), Is.EqualTo(saved));
    }

    [Test]
    public void Onto_ShrinksAWindowTooBigForTheScreenItLandsOn()
    {
        var saved = Placement(4000, 100, 3000, 1800);

        var fitted = WindowPlacementFit.Onto(saved, [Second])!;

        Assert.Multiple(() =>
        {
            Assert.That(fitted.Bounds.Width, Is.LessThanOrEqualTo(Second.Width));
            Assert.That(fitted.Bounds.Height, Is.LessThanOrEqualTo(Second.Height));
            Assert.That(fitted.Bounds.X, Is.GreaterThanOrEqualTo(Second.X));
        });
    }

    [Test]
    public void Onto_KeepsTheSideOfTheScreenTheWindowWasOn()
    {
        var saved = Placement(5000, 100, 800, 600);

        var fitted = WindowPlacementFit.Onto(saved, [Laptop])!;

        Assert.That(fitted.Bounds.X + fitted.Bounds.Width, Is.EqualTo(Laptop.Width),
            "somebody who put the window on the right still wants it on the right");
    }

    [Test]
    public void Onto_KeepsWhetherItWasMaximised()
    {
        var saved = new WindowPlacement(new LayoutRect(4000, 200, 1400, 900), IsMaximised: true);

        Assert.That(WindowPlacementFit.Onto(saved, [Laptop])!.IsMaximised, Is.True);
    }

    [Test]
    public void Onto_GivesNothingBackWhenThereIsNothingSaved()
    {
        Assert.That(WindowPlacementFit.Onto(null, [Laptop]), Is.Null);
    }

    [Test]
    public void Onto_GivesNothingBackWhenNoScreenCanBeMeasured()
    {
        Assert.That(WindowPlacementFit.Onto(Placement(0, 0, 800, 600), []), Is.Null,
            "with nothing to check against, the caller's own default is the honest answer");
    }

    [Test]
    public void Onto_GivesNothingBackForASavedSizeOfNothing()
    {
        Assert.That(WindowPlacementFit.Onto(Placement(0, 0, 0, 0), [Laptop]), Is.Null);
    }

    [Test]
    public void IsReachable_RefusesAWindowWhoseTitleBarIsAboveTheTopOfTheScreen()
    {
        Assert.That(WindowPlacementFit.IsReachable(new LayoutRect(200, -80, 1400, 900), [Laptop]), Is.False,
            "most of it is visible and there is nothing to take hold of");
    }

    [Test]
    public void IsReachable_RefusesAWindowShowingOnlyACorner()
    {
        Assert.That(WindowPlacementFit.IsReachable(new LayoutRect(3400, 2000, 1400, 900), [Laptop]), Is.False);
    }

    [Test]
    public void IsReachable_AcceptsAWindowHangingOffTheRightButStillGrabbable()
    {
        Assert.That(WindowPlacementFit.IsReachable(new LayoutRect(3000, 100, 800, 600), [Laptop]), Is.True);
    }

    private static WindowPlacement Placement(int x, int y, int width, int height) =>
        new(new LayoutRect(x, y, width, height), IsMaximised: false);
}
