using Theseus.Core.Links;

namespace Theseus.Tests.Links;

[TestFixture]
public class LinkReportStatusTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void StateOf_AnUnsatisfiedLinkWithAReportIsReported()
    {
        var link = Feeds() with { LatestReport = Report() };

        Assert.That(Status(link, Card("spike"), Card("research")).StateOf(link), Is.EqualTo(LinkState.Reported));
    }

    [Test]
    public void StateOf_AReportNeverSatisfiesALinkUntilItIsConfirmed()
    {
        var reported = Feeds() with { LatestReport = Report() };
        var confirmed = reported with { SatisfiedUtc = Nine.AddHours(1), SatisfiedBy = LinkBook.ConfirmedReport };

        Assert.That(Status(reported, Card("spike"), Card("research")).ReadinessOf("research"), Is.EqualTo(CardReadiness.Waiting));
        Assert.That(Status(confirmed, Card("spike"), Card("research")).StateOf(confirmed), Is.EqualTo(LinkState.Satisfied));
    }

    [Test]
    public void StateOf_AnUpstreamThatRanSinceAndStoppedCleanlyHasNoReportYet()
    {
        var link = Feeds();

        Assert.That(Status(link, Card("spike", lastRun: Nine.AddHours(1)), Card("research")).StateOf(link),
            Is.EqualTo(LinkState.NoReportYet));
    }

    [Test]
    public void StateOf_AnUpstreamStillRunningIsOnlyWaiting()
    {
        var link = Feeds();

        Assert.That(Status(link, Card("spike", live: true, lastRun: Nine.AddHours(1)), Card("research")).StateOf(link),
            Is.EqualTo(LinkState.Waiting));
    }

    [Test]
    public void StateOf_RunsFromBeforeTheLinkWasMadeDoNotCount()
    {
        var link = Feeds();

        Assert.That(Status(link, Card("spike", lastRun: Nine.AddHours(-1), crashed: true), Card("research")).StateOf(link),
            Is.EqualTo(LinkState.Waiting));
    }

    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    public void StateOf_AnUpstreamFinishedWithAndNoReportIsReportLost(bool archived, bool done, bool crashed)
    {
        var link = Feeds();
        var upstream = Card("spike", lastRun: Nine.AddHours(1), archived: archived, done: done, crashed: crashed);

        Assert.That(Status(link, upstream, Card("research")).StateOf(link), Is.EqualTo(LinkState.ReportLost));
    }

    private static LinkStatus Status(Link link, params LinkCard[] cards) => new([link], cards);

    private static LinkCard Card(string key, bool live = false, DateTimeOffset? lastRun = null, bool archived = false,
        bool done = false, bool crashed = false) =>
        new(key, key, archived, false, live, lastRun, done, crashed);

    private static Link Feeds() =>
        new("lk-1", LinkKind.Feeds, "spike", "research", Nine, Link.MadeOnTheBoard, Output: @"C:\work\REPORT.md");

    private static LinkReport Report() => new(Nine.AddMinutes(30), "session-1", @"C:\work\REPORT.md", "Found it.", "go");
}
