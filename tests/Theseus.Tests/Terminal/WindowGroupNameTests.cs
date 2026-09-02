using Theseus.Core.Domain;
using Theseus.Core.Terminal;

namespace Theseus.Tests.Terminal;

[TestFixture]
public class WindowGroupNameTests
{
    [Test]
    public void For_GivesEachLaneAWindowOfItsOwn()
    {
        Assert.That(WindowGroupName.For("theseus", Workstream("In flight")),
            Is.EqualTo("theseus-in-flight"),
            "the lanes are already the board's grouping of the work, so the screen mirrors them");
    }

    [Test]
    public void For_PutsTwoWorkstreamsInTheSameLaneInTheSameWindow()
    {
        Assert.That(WindowGroupName.For("theseus", Workstream("Today")),
            Is.EqualTo(WindowGroupName.For("theseus", Workstream("Today") with { Key = "another" })));
    }

    [Test]
    public void For_LetsAWorkstreamNameItsOwnWindowWhateverLaneItIsIn()
    {
        var workstream = Workstream("In flight") with { WindowGroup = "Side by side" };

        Assert.That(WindowGroupName.For("theseus", workstream), Is.EqualTo("side-by-side"),
            "naming a window is asking for one, and beats what the lane would have said");
    }

    [Test]
    public void For_FallsBackWhenThereIsNoConfiguredName()
    {
        Assert.That(WindowGroupName.For("   ", Workstream("Today")), Is.EqualTo("theseus-today"));
    }

    [Test]
    public void For_FallsBackWhenALaneNameSurvivesSanitisingAsNothing()
    {
        Assert.That(WindowGroupName.For("theseus", Workstream("···")), Is.EqualTo("theseus"),
            "a window named for nothing is worse than everything landing together");
    }

    [Test]
    public void For_FallsBackWhenAnOverrideSurvivesSanitisingAsNothing()
    {
        var workstream = Workstream("Today") with { WindowGroup = "///" };

        Assert.That(WindowGroupName.For("theseus", workstream), Is.EqualTo("theseus"));
    }

    [TestCase("In flight", "in-flight")]
    [TestCase("To Do / Next", "to-do-next")]
    [TestCase("  Today  ", "today")]
    [TestCase("Done!!", "done")]
    [TestCase("Side Project", "side-project")]
    [TestCase("", "")]
    public void Sanitise_ReducesALaneNameToSomethingSafeToPass(string given, string expected)
    {
        Assert.That(WindowGroupName.Sanitise(given), Is.EqualTo(expected));
    }

    private static Workstream Workstream(string lane) => new(
        "contact-forms", "Contact forms", "#336699", ColorIsManual: false, @"C:\work\repo", [],
        "prompt", 1, lane, IsBlocked: false, ReopenTomorrow: false, IsArchived: false,
        LastSessionId: null, [], StatusDocumentPath: null, DateTimeOffset.UtcNow);

    [Test]
    public void For_NamesTheWindowAfterTheAreaRatherThanTheStage()
    {
        var workstream = Workstream("In progress") with { Area = "Research" };

        Assert.That(WindowGroupName.For("theseus", workstream), Is.EqualTo("theseus-research"),
            "a window named for a stage would want renaming every time the work advanced");
    }

    [Test]
    public void For_KeepsTheSameWindowAsWorkMovesAcrossTheBoard()
    {
        var research = Workstream("New") with { Area = "Research" };

        Assert.That(WindowGroupName.For("theseus", research),
            Is.EqualTo(WindowGroupName.For("theseus", research with { Column = "Done" })),
            "the session cannot move between windows, so the name must not move either");
    }

    [Test]
    public void For_FallsBackToTheLaneOnABoardWithNoAreasYet()
    {
        Assert.That(WindowGroupName.For("theseus", Workstream("Side Project")),
            Is.EqualTo("theseus-side-project"));
    }
}
