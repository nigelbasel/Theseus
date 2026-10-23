using Theseus.Core.Links;

namespace Theseus.Tests.Links;

[TestFixture]
public class LinkStatusTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void StateOf_AnUnacceptedHandOverIsWaiting()
    {
        var status = Status([Feeds("spike", "implementation")], Card("spike"), Card("implementation"));

        Assert.That(status.StateOf(Feeds("spike", "implementation")), Is.EqualTo(LinkState.Waiting));
    }

    [Test]
    public void StateOf_AnAcceptedHandOverIsSatisfied()
    {
        var link = Feeds("spike", "implementation", satisfied: Nine);

        Assert.That(Status([link], Card("spike"), Card("implementation")).StateOf(link),
            Is.EqualTo(LinkState.Satisfied));
    }

    [Test]
    public void StateOf_AHandOverWithNoDocumentIsLineage()
    {
        var link = Lineage("research", "spike");

        Assert.That(Status([link], Card("research"), Card("spike")).StateOf(link), Is.EqualTo(LinkState.Lineage));
    }

    [TestCase("spike")]
    [TestCase("implementation")]
    public void StateOf_ALinkWithACardGoneIsCardMissing(string present)
    {
        var link = Feeds("spike", "implementation", satisfied: Nine);

        Assert.That(Status([link], Card(present)).StateOf(link), Is.EqualTo(LinkState.CardMissing));
    }

    [Test]
    public void ReadinessOf_ACardWithNoInputsIsUnlinked()
    {
        var status = Status([Lineage("research", "spike")], Card("research"), Card("spike"));

        Assert.That(status.ReadinessOf("spike"), Is.EqualTo(CardReadiness.None));
    }

    [Test]
    public void ReadinessOf_AReportBackNeverHoldsTheParentBack()
    {
        var status = Status([Lineage("research", "spike"), Feeds("spike", "research") with { Kind = LinkKind.ReportsBack }],
            Card("research"), Card("spike"));

        Assert.That(status.ReadinessOf("research"), Is.EqualTo(CardReadiness.None));
        Assert.That(status.Inputs("research"), Is.Empty);
    }

    [Test]
    public void ReadinessOf_ACardWhoseHandOverIsAcceptedIsReady()
    {
        var status = Status([Feeds("one", "implementation", satisfied: Nine)], Card("one"), Card("implementation"));

        Assert.That(status.ReadinessOf("implementation"), Is.EqualTo(CardReadiness.Ready));
    }

    [Test]
    public void ReadinessOf_ACardWhoseHandOverIsOutstandingIsWaiting()
    {
        var status = Status([Feeds("one", "implementation")], Card("one"), Card("implementation"));

        Assert.That(status.ReadinessOf("implementation"), Is.EqualTo(CardReadiness.Waiting));
    }

    [Test]
    public void ReadinessOf_ALiveCardIsNeitherReadyNorWaiting()
    {
        var status = Status([Feeds("one", "implementation")], Card("one"), Card("implementation", live: true));

        Assert.That(status.ReadinessOf("implementation"), Is.EqualTo(CardReadiness.None));
    }

    [Test]
    public void ReadinessOf_AnArchivedCardIsNeverReady()
    {
        var status = Status([Feeds("one", "implementation", satisfied: Nine)],
            Card("one"), Card("implementation", archived: true));

        Assert.That(status.ReadinessOf("implementation"), Is.EqualTo(CardReadiness.None));
    }

    [Test]
    public void ReadinessOf_ABlockedCardIsNeverReady()
    {
        var status = Status([Feeds("one", "implementation", satisfied: Nine)],
            Card("one"), Card("implementation", blocked: true));

        Assert.That(status.ReadinessOf("implementation"), Is.EqualTo(CardReadiness.None));
    }

    [Test]
    public void ReadinessOf_ACardThatRanSinceItsInputsWereSatisfiedIsNoLongerReady()
    {
        var status = Status([Feeds("one", "implementation", satisfied: Nine)],
            Card("one"), Card("implementation", lastRun: Nine.AddHours(1)));

        Assert.That(status.ReadinessOf("implementation"), Is.EqualTo(CardReadiness.None));
    }

    [Test]
    public void ReadinessOf_ACardThatRanOnlyBeforeItsInputsWereSatisfiedIsReady()
    {
        var status = Status([Feeds("one", "implementation", satisfied: Nine)],
            Card("one"), Card("implementation", lastRun: Nine.AddHours(-1)));

        Assert.That(status.ReadinessOf("implementation"), Is.EqualTo(CardReadiness.Ready));
    }

    [Test]
    public void ReadinessOf_IgnoresRemovedLinks()
    {
        var status = Status([Feeds("one", "implementation") with { RemovedUtc = Nine }],
            Card("one"), Card("implementation"));

        Assert.That(status.ReadinessOf("implementation"), Is.EqualTo(CardReadiness.None));
    }

    [Test]
    public void WaitingOn_NamesTheParentWhileItsHandOverIsOutstanding()
    {
        var status = Status([Feeds("one", "implementation")], Card("one"), Card("implementation"));

        Assert.That(status.WaitingOn("implementation"), Is.EqualTo(new[] { "one" }));
    }

    private static LinkStatus Status(IReadOnlyList<Link> links, params LinkCard[] cards) => new(links, cards);

    private static LinkCard Card(string key, bool live = false, bool archived = false, bool blocked = false,
        DateTimeOffset? lastRun = null) =>
        new(key, key, archived, blocked, live, lastRun);

    private static Link Feeds(string from, string to, DateTimeOffset? satisfied = null) =>
        new($"lk-{from}-{to}", LinkKind.HandsOver, from, to, Nine.AddDays(-1), Link.MadeOnTheBoard,
            Output: $@"C:\work\{from}\REPORT.md", SatisfiedUtc: satisfied, SatisfiedBy: satisfied is null ? null : "by hand: checked");

    private static Link Lineage(string from, string to) =>
        new($"lk-{from}-{to}", LinkKind.HandsOver, from, to, Nine.AddDays(-1), Link.MadeOnTheBoard);
}
