using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Tests.Links;

[TestFixture]
public class WaitingStartsTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void Among_HoldsBackOnlyTheCardsStillWaitingOnAnInput()
    {
        var status = Status(
            Feeds("token-spike", "auth-implementation"),
            Feeds("cache-spike", "cache-fix", satisfied: true));

        var held = WaitingStarts.Among(
            [Workstream("auth-implementation"), Workstream("cache-fix"), Workstream("unlinked")], status);

        Assert.That(held.Select(workstream => workstream.Key), Is.EqualTo(new[] { "auth-implementation" }));
    }

    [Test]
    public void Summary_NamesEachHeldCardAndWhatItWaitsOn()
    {
        var status = Status(Feeds("token-spike", "auth-implementation"), Feeds("cache-spike", "cache-fix"));

        var summary = WaitingStarts.Summary([Workstream("auth-implementation"), Workstream("cache-fix")], status);

        Assert.That(summary, Is.EqualTo(
            "2 cards are waiting on inputs: Auth implementation (on Token spike), Cache fix (on Cache spike)."));
    }

    [Test]
    public void Summary_SpeaksOfOneCardInTheSingular()
    {
        var summary = WaitingStarts.Summary([Workstream("auth-implementation")],
            Status(Feeds("token-spike", "auth-implementation")));

        Assert.That(summary, Is.EqualTo("Auth implementation is waiting on an input (on Token spike)."));
    }

    private static LinkStatus Status(params Link[] links)
    {
        var keys = links.SelectMany(link => new[] { link.From, link.To }).Append("unlinked").Distinct();

        return new LinkStatus(links, keys.Select(key => new LinkCard(key, Title(key), false, false, false, null)));
    }

    private static Workstream Workstream(string key) =>
        new(key, Title(key), "#336699", false, @"C:\work", [], "Go", 0, "New", false, false, false, null, [], null, Nine);

    private static string Title(string key)
    {
        string words = key.Replace('-', ' ');

        return char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static Link Feeds(string from, string to, bool satisfied = false) =>
        new($"lk-{from}-{to}", LinkKind.Feeds, from, to, Nine, Link.MadeOnTheBoard,
            SatisfiedUtc: satisfied ? Nine.AddHours(1) : null, SatisfiedBy: satisfied ? "by hand: ok" : null);
}
