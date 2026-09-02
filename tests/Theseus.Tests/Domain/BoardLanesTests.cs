using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class BoardLanesTests
{
    private static readonly string[] Lanes = ["Today", "In flight", "Parked", "Done"];

    [Test]
    public void Resolve_KeepsACardInTheLaneItRecords()
    {
        Assert.That(BoardLanes.Resolve("Parked", Lanes), Is.EqualTo("Parked"));
    }

    [Test]
    public void Resolve_ReadsALaneWrittenBeforeLanesHadNames()
    {
        Assert.That(BoardLanes.Resolve("InFlight", Lanes), Is.EqualTo("In flight"),
            "boards written when the lanes were an enum must not lose their cards");
    }

    [Test]
    public void Resolve_IgnoresCaseAndPunctuationSoRenamingALaneDoesNotEmptyIt()
    {
        Assert.That(BoardLanes.Resolve("in-flight", Lanes), Is.EqualTo("In flight"));
        Assert.That(BoardLanes.Resolve("IN FLIGHT", Lanes), Is.EqualTo("In flight"));
    }

    [Test]
    public void Resolve_PutsACardWhoseLaneWasDeletedInTheFirstLaneRatherThanNowhere()
    {
        Assert.That(BoardLanes.Resolve("Blocked", Lanes), Is.EqualTo("Today"));
    }

    [Test]
    public void Resolve_PlacesACardThatHasNeverBeenInALane()
    {
        Assert.That(BoardLanes.Resolve(null, Lanes), Is.EqualTo("Today"));
        Assert.That(BoardLanes.Resolve(string.Empty, Lanes), Is.EqualTo("Today"));
    }

    [Test]
    public void Sanitise_DropsTheBlankLinesLeftBehindWhileTyping()
    {
        Assert.That(BoardLanes.Sanitise(["Today", "   ", "Done"]), Is.EqualTo(new[] { "Today", "Done" }));
    }

    [Test]
    public void Sanitise_TrimsLaneNames()
    {
        Assert.That(BoardLanes.Sanitise(["  Today  "]), Is.EqualTo(new[] { "Today" }));
    }

    [Test]
    public void Sanitise_RefusesTwoLanesThatWouldBeIndistinguishable()
    {
        Assert.That(BoardLanes.Sanitise(["In flight", "in-flight"]), Is.EqualTo(new[] { "In flight" }),
            "cards resolve by loose name, so two lanes matching loosely would compete for the same cards");
    }

    [Test]
    public void Sanitise_KeepsTheOrderTheLanesWereGivenIn()
    {
        Assert.That(BoardLanes.Sanitise(["Done", "Today"]), Is.EqualTo(new[] { "Done", "Today" }));
    }

    [Test]
    public void Sanitise_FallsBackToTheDefaultsRatherThanLeavingABoardWithNowhereToPutACard()
    {
        Assert.That(BoardLanes.Sanitise([]), Is.EqualTo(BoardLanes.Default));
        Assert.That(BoardLanes.Sanitise(null), Is.EqualTo(BoardLanes.Default));
        Assert.That(BoardLanes.Sanitise(["  "]), Is.EqualTo(BoardLanes.Default));
    }

    [Test]
    public void Sanitise_AllowsAsManyLanesAsAreWanted()
    {
        var many = Enumerable.Range(1, 9).Select(number => $"Lane {number}").ToList();

        Assert.That(BoardLanes.Sanitise(many), Is.EqualTo(many));
    }

    [Test]
    public void Resolve_CopesWithABoardConfiguredWithNoLanes()
    {
        Assert.That(BoardLanes.Resolve("Today", []), Is.EqualTo(BoardLanes.Default[0]));
    }
}
