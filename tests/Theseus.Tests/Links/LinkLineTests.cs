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
            HandOver("auth-research", "auth-implementation", satisfied: true),
        ]));

        Assert.That(line.Text, Is.EqualTo("Ready · ↑ from Auth research"));
        Assert.That(line.IsReady, Is.True);
    }

    [Test]
    public void For_NamesTheHandOverAWaitingCardWaitsOn()
    {
        var line = LinkLine.For("auth-implementation", Status(
        [
            HandOver("auth-design", "auth-implementation"),
        ]));

        Assert.That(line.Text, Is.EqualTo("⧗ waits on Auth design · ↑ from Auth design"));
        Assert.That(line.IsReady, Is.False);
    }

    [Test]
    public void For_AHandOverWithNoDocumentHoldsNothingBack()
    {
        var line = LinkLine.For("token-spike", Status(
        [
            Lineage("auth-research", "token-spike"),
        ]));

        Assert.That(line.Text, Is.EqualTo("↑ from Auth research"));
    }

    [Test]
    public void For_NamesTheCardsAParentHandsOverTo()
    {
        var line = LinkLine.For("auth-research", Status(
        [
            Lineage("auth-research", "token-spike"),
            Lineage("auth-research", "cache-spike"),
            Lineage("auth-research", "session-spike"),
        ]));

        Assert.That(line.Text, Is.EqualTo("→ Token spike, Cache spike +1"));
    }

    [Test]
    public void For_SaysWhomAChildReportsBackTo()
    {
        var line = LinkLine.For("token-spike", Status(
        [
            Lineage("auth-research", "token-spike"),
            ReportBack("token-spike", "auth-research"),
        ]));

        Assert.That(line.Text, Is.EqualTo("↑ from Auth research · ↩ reports back to Auth research"));
    }

    [Test]
    public void For_CountsAReportBackWaitingForConfirmOnTheParent()
    {
        var line = LinkLine.For("auth-research", Status(
        [
            Lineage("auth-research", "token-spike"),
            ReportBack("token-spike", "auth-research") with
            {
                LatestReport = new LinkReport(Nine.AddHours(2), "s-1", @"C:\work\REPORT.md", "Done.", "done"),
            },
        ]));

        Assert.That(line.Text, Is.EqualTo("? 1 to confirm · → Token spike"));
    }

    [Test]
    public void For_SaysWhenALinkedCardIsMissing()
    {
        var links = new[] { Lineage("auth-research", "deleted-spike") };
        var status = new LinkStatus(links, [new LinkCard("auth-research", "Auth research", false, false, false, null)]);

        Assert.That(LinkLine.For("auth-research", status).Text, Does.Contain("linked card missing"));
    }

    [Test]
    public void For_ListsEveryLinkInTheTooltip()
    {
        var line = LinkLine.For("token-spike", Status(
        [
            HandOver("auth-research", "token-spike"),
            ReportBack("token-spike", "auth-research"),
        ]));

        Assert.That(line.Tooltip, Does.Contain("Takes over from Auth research: not accepted yet")
            .And.Contain("Reports back to Auth research: not accepted yet"));
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

    private static Link HandOver(string from, string to, bool satisfied = false) =>
        new($"lk-{from}-{to}", LinkKind.HandsOver, from, to, Nine, Link.MadeOnTheBoard, Output: @"C:\work\HANDOVER.md",
            SatisfiedUtc: satisfied ? Nine.AddHours(1) : null, SatisfiedBy: satisfied ? "by hand: ok" : null);

    private static Link Lineage(string from, string to) =>
        new($"lk-l-{from}-{to}", LinkKind.HandsOver, from, to, Nine, Link.MadeOnTheBoard);

    private static Link ReportBack(string from, string to) =>
        new($"lk-r-{from}-{to}", LinkKind.ReportsBack, from, to, Nine, Link.MadeOnTheBoard, Output: @"C:\work\REPORT.md");
}
