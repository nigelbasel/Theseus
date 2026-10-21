using Theseus.Core.Links;

namespace Theseus.Tests.Links;

[TestFixture]
public class LinkLineTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void For_IsEmptyForAnUnlinkedCard()
    {
        Assert.That(LinkLine.For("lonely", Status([])).Text, Is.Empty);
    }

    [Test]
    public void For_SaysReadyFirstAndWhereTheCardCameFrom()
    {
        var line = LinkLine.For("auth-implementation", Status(
        [
            Spawned("auth-research", "auth-implementation"),
            Feeds("token-spike", "auth-implementation", satisfied: true),
        ]));

        Assert.That(line.Text, Is.EqualTo("Ready · ↑ from Auth research"));
        Assert.That(line.IsReady, Is.True);
    }

    [Test]
    public void For_NamesWhatAWaitingCardWaitsOn()
    {
        var line = LinkLine.For("auth-implementation", Status(
        [
            Feeds("token-spike", "auth-implementation"),
            Feeds("cache-spike", "auth-implementation", satisfied: true),
        ]));

        Assert.That(line.Text, Is.EqualTo("⧗ waits on Token spike"));
        Assert.That(line.IsReady, Is.False);
    }

    [Test]
    public void For_ShortensALongListOfInputs()
    {
        var line = LinkLine.For("implementation", Status(
        [
            Feeds("one", "implementation"),
            Feeds("two", "implementation"),
            Feeds("three", "implementation"),
        ]));

        Assert.That(line.Text, Is.EqualTo("⧗ waits on One, Two +1"));
    }

    [Test]
    public void For_CountsAParentsChildren()
    {
        var line = LinkLine.For("auth-research", Status(
        [
            Spawned("auth-research", "token-spike"),
            Spawned("auth-research", "cache-spike"),
        ]));

        Assert.That(line.Text, Is.EqualTo("⑂ 2 children"));
    }

    [Test]
    public void For_SaysWhatAnUpstreamFeeds()
    {
        var line = LinkLine.For("token-spike", Status(
        [
            Spawned("auth-research", "token-spike"),
            Feeds("token-spike", "auth-research"),
        ]));

        Assert.That(line.Text, Is.EqualTo("↑ from Auth research · → Auth research"));
    }

    [Test]
    public void For_SaysWhatWasHandedOver()
    {
        var line = LinkLine.For("auth-research", Status(
        [
            Spawned("auth-research", "token-spike") with { HandOver = "token lifetimes" },
        ]));

        Assert.That(line.Text, Is.EqualTo("⑂ 1 child · handed over to Token spike"));
    }

    [Test]
    public void For_SaysWhenALinkedCardIsMissing()
    {
        var links = new[] { Spawned("auth-research", "deleted-spike") };
        var status = new LinkStatus(links, [new LinkCard("auth-research", "Auth research", false, false, false, null)]);

        Assert.That(LinkLine.For("auth-research", status).Text, Does.Contain("linked card missing"));
    }

    [Test]
    public void For_ListsEveryLinkInTheTooltip()
    {
        var line = LinkLine.For("token-spike", Status(
        [
            Spawned("auth-research", "token-spike"),
            Feeds("token-spike", "auth-research"),
        ]));

        Assert.That(line.Tooltip, Does.Contain("Spawned by Auth research").And.Contain("Auth research waits on this"));
    }

    private static LinkStatus Status(IReadOnlyList<Link> links)
    {
        var keys = links.SelectMany(link => new[] { link.From, link.To }).Append("lonely").Distinct();

        return new LinkStatus(links, keys.Select(key => new LinkCard(key, Title(key), false, false, false, null)));
    }

    private static string Title(string key)
    {
        string words = key.Replace('-', ' ');

        return char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static Link Feeds(string from, string to, bool satisfied = false) =>
        new($"lk-{from}-{to}", LinkKind.Feeds, from, to, Nine, Link.MadeOnTheBoard,
            SatisfiedUtc: satisfied ? Nine.AddHours(1) : null, SatisfiedBy: satisfied ? "by hand: ok" : null);

    private static Link Spawned(string from, string to) =>
        new($"lk-s-{from}-{to}", LinkKind.Spawned, from, to, Nine, Link.MadeOnTheBoard);
}
