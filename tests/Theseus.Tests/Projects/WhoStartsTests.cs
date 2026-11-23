using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// In a running project a box says who starts it and when, and offers Start anyway… second (project-view-usability
/// item 5, Nigel 2026-11-23).
/// </summary>
[TestFixture]
public class WhoStartsTests
{
    [Test]
    public void Line_NamesTheLiveLead_AndWhenItsTurnComes()
    {
        Assert.That(WhoStarts.Line("BuildYourMarket MCP", "Spec", heldByOwner: false, []),
            Is.EqualTo("Spec starts it, when the project's plan reaches it: BuildYourMarket MCP is running and decides what runs."));
    }

    [Test]
    public void Line_NamesTheCardWhoseHandOverItWaitsFor()
    {
        Assert.That(WhoStarts.Line("P", "Lead", heldByOwner: false, ["W1"]), Does.Contain("when W1 hands work over to it"));
    }

    [Test]
    public void Line_DoesNotNameALeadThatHasNoSession()
    {
        Assert.That(WhoStarts.Line("P", null, heldByOwner: false, []), Does.StartWith("P's sessions start it"));
    }

    [Test]
    public void Line_IsNothing_OutsideARunningProject_OrWhereTheOwnerHoldsIt()
    {
        Assert.Multiple(() =>
        {
            Assert.That(WhoStarts.Line(null, "Lead", heldByOwner: false, []), Is.Null, "a stopped project or an ad hoc card");
            Assert.That(WhoStarts.Line("P", "Lead", heldByOwner: true, []), Is.Null, "paused, blocked or stopped by him");
        });
    }
}
