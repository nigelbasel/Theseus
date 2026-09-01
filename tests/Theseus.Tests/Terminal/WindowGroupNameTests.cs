using Theseus.Core.Domain;
using Theseus.Core.Terminal;

namespace Theseus.Tests.Terminal;

[TestFixture]
public class WindowGroupNameTests
{
    [Test]
    public void For_PutsEverythingInOneWindowUnlessAskedOtherwise()
    {
        Assert.That(WindowGroupName.For(WindowGrouping.OneWindow, "theseus", Workstream("In flight")),
            Is.EqualTo("theseus"),
            "what Theseus has always done, and what it keeps doing for anyone who has not asked");
    }

    [Test]
    public void For_GivesEachLaneAWindowOfItsOwnWhenAsked()
    {
        Assert.That(WindowGroupName.For(WindowGrouping.WindowPerLane, "theseus", Workstream("In flight")),
            Is.EqualTo("theseus-in-flight"));
    }

    [Test]
    public void For_LetsAWorkstreamNameItsOwnWindowWhateverLaneItIsIn()
    {
        var workstream = Workstream("In flight") with { WindowGroup = "Side by side" };

        Assert.That(WindowGroupName.For(WindowGrouping.WindowPerLane, "theseus", workstream),
            Is.EqualTo("side-by-side"));
    }

    [Test]
    public void For_HonoursAnOverrideEvenWhenLanesAreNotGrouping()
    {
        var workstream = Workstream("In flight") with { WindowGroup = "watching" };

        Assert.That(WindowGroupName.For(WindowGrouping.OneWindow, "theseus", workstream),
            Is.EqualTo("watching"),
            "naming a window is asking for one, whatever the general setting says");
    }

    [Test]
    public void For_FallsBackWhenThereIsNoConfiguredName()
    {
        Assert.That(WindowGroupName.For(WindowGrouping.OneWindow, "   ", Workstream("Today")),
            Is.EqualTo("theseus"));
    }

    [Test]
    public void For_FallsBackWhenALaneNameSurvivesSanitisingAsNothing()
    {
        Assert.That(WindowGroupName.For(WindowGrouping.WindowPerLane, "theseus", Workstream("···")),
            Is.EqualTo("theseus"),
            "a window named for nothing is worse than one window");
    }

    [TestCase("In flight", "in-flight")]
    [TestCase("To Do / Next", "to-do-next")]
    [TestCase("  Today  ", "today")]
    [TestCase("Done!!", "done")]
    [TestCase("", "")]
    public void Sanitise_ReducesALaneNameToSomethingSafeToPass(string given, string expected)
    {
        Assert.That(WindowGroupName.Sanitise(given), Is.EqualTo(expected));
    }

    private static Workstream Workstream(string lane) => new(
        "contact-forms", "Contact forms", "#336699", ColorIsManual: false, @"C:\work\repo", [],
        "prompt", 1, lane, IsBlocked: false, ReopenTomorrow: false, IsArchived: false,
        LastSessionId: null, [], StatusDocumentPath: null, DateTimeOffset.UtcNow);
}
