using Theseus.Core.Abstractions;
using Theseus.Core.Links;

namespace Theseus.Tests.Links;

[TestFixture]
public class LinkBookTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private MovableClock _clock = null!;
    private LinkBook _book = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-links-{Guid.NewGuid():N}");
        _clock = new MovableClock(Nine);
        _book = Open();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void Add_RecordsALinkMadeOnTheBoard()
    {
        var change = _book.Add(LinkKind.HandsOver, "auth-design", "auth-implementation", output: @"C:\work\DESIGN.md");

        Assert.That(change.Refusal, Is.Null);
        Assert.That(change.Link!.Kind, Is.EqualTo(LinkKind.HandsOver));
        Assert.That(change.Link.From, Is.EqualTo("auth-design"));
        Assert.That(change.Link.To, Is.EqualTo("auth-implementation"));
        Assert.That(change.Link.Output, Is.EqualTo(@"C:\work\DESIGN.md"));
        Assert.That(change.Link.CreatedUtc, Is.EqualTo(Nine));
        Assert.That(change.Link.CreatedBy, Is.EqualTo(Link.MadeOnTheBoard));
        Assert.That(_book.Active, Has.Count.EqualTo(1));
    }

    [Test]
    public void Add_RefusesALinkFromACardToItself()
    {
        var change = _book.Add(LinkKind.HandsOver, "auth-research", "auth-research");

        Assert.That(change.Link, Is.Null);
        Assert.That(change.Refusal, Does.Contain("itself"));
    }

    [Test]
    public void Add_RefusesTheSameLinkTwice()
    {
        _book.Add(LinkKind.HandsOver, "auth-design", "auth-implementation");

        var change = _book.Add(LinkKind.HandsOver, "auth-design", "auth-implementation");

        Assert.That(change.Refusal, Does.Contain("already"));
        Assert.That(_book.Active, Has.Count.EqualTo(1));
    }

    [Test]
    public void Add_AcceptsAParentWithSeveralChildren()
    {
        _book.Add(LinkKind.HandsOver, "research", "spike-one");

        var change = _book.Add(LinkKind.HandsOver, "research", "spike-two");

        Assert.That(change.Refusal, Is.Null);
    }

    [Test]
    public void Add_RefusesASecondParent()
    {
        _book.Add(LinkKind.HandsOver, "research", "spike");

        var change = _book.Add(LinkKind.HandsOver, "other-research", "spike");

        Assert.That(change.Refusal, Does.Contain("research already hands over to spike"));
    }

    [Test]
    public void Add_RefusesAHandOverThatMakesACardItsOwnAncestor()
    {
        _book.Add(LinkKind.HandsOver, "research", "design");
        _book.Add(LinkKind.HandsOver, "design", "implementation");

        var change = _book.Add(LinkKind.HandsOver, "implementation", "research");

        Assert.That(change.Refusal, Does.Contain("ancestor"));
    }

    [Test]
    public void Add_AcceptsAReportBackToTheCardThatHandedOver()
    {
        _book.Add(LinkKind.HandsOver, "research", "spike");

        var change = _book.Add(LinkKind.ReportsBack, "spike", "research", output: @"C:\work\REPORT.md");

        Assert.That(change.Refusal, Is.Null);
    }

    [Test]
    public void Add_RefusesAReportBackToACardThatNeverHandedOver()
    {
        _book.Add(LinkKind.HandsOver, "research", "spike");

        var change = _book.Add(LinkKind.ReportsBack, "research", "spike");

        Assert.That(change.Refusal, Does.Contain("spike does not hand over to research"));
    }

    [Test]
    public void Add_IgnoresRemovedLinksWhenLookingForAParent()
    {
        var first = _book.Add(LinkKind.HandsOver, "a", "b").Link!;
        _book.Remove(first.Id);

        var change = _book.Add(LinkKind.HandsOver, "c", "b");

        Assert.That(change.Refusal, Is.Null);
    }

    [Test]
    public void Remove_KeepsTheLinkAsATombstone()
    {
        var link = _book.Add(LinkKind.HandsOver, "a", "b").Link!;
        _clock.Advance(TimeSpan.FromMinutes(5));

        _book.Remove(link.Id);

        Assert.That(_book.Active, Is.Empty);
        Assert.That(_book.All.Single().RemovedUtc, Is.EqualTo(Nine.AddMinutes(5)));
    }

    [Test]
    public void Remove_OfAHandOverTakesItsReportBackWithIt()
    {
        var handOver = _book.Add(LinkKind.HandsOver, "research", "spike").Link!;
        _book.Add(LinkKind.ReportsBack, "spike", "research", output: @"C:\work\REPORT.md");

        _book.Remove(handOver.Id);

        Assert.That(_book.Active, Is.Empty);
        Assert.That(_book.All, Has.Count.EqualTo(2));
    }

    [Test]
    public void Satisfy_RecordsTheReasonGivenByHand()
    {
        var link = _book.Add(LinkKind.HandsOver, "a", "b", output: @"C:\work\DESIGN.md").Link!;
        _clock.Advance(TimeSpan.FromMinutes(5));

        var change = _book.Satisfy(link.Id, "Read the spike's handoff");

        Assert.That(change.Link!.SatisfiedUtc, Is.EqualTo(Nine.AddMinutes(5)));
        Assert.That(change.Link.SatisfiedBy, Is.EqualTo("by hand: Read the spike's handoff"));
    }

    [Test]
    public void Satisfy_RefusesWithoutAReason()
    {
        var link = _book.Add(LinkKind.HandsOver, "a", "b", output: @"C:\work\DESIGN.md").Link!;

        var change = _book.Satisfy(link.Id, "  ");

        Assert.That(change.Refusal, Does.Contain("reason"));
        Assert.That(_book.Active.Single().SatisfiedUtc, Is.Null);
    }

    [Test]
    public void Satisfy_RefusesALinkThatNamesNoDocument()
    {
        var link = _book.Add(LinkKind.HandsOver, "a", "b").Link!;

        var change = _book.Satisfy(link.Id, "done");

        Assert.That(change.Refusal, Does.Contain("no document"));
    }

    [Test]
    public void RemoveAllFor_TombstonesEveryLinkTouchingADeletedCard()
    {
        _book.Add(LinkKind.HandsOver, "research", "spike");
        _book.Add(LinkKind.ReportsBack, "spike", "research");
        _book.Add(LinkKind.HandsOver, "other", "elsewhere");

        _book.RemoveAllFor("spike");

        Assert.That(_book.Active.Select(link => link.From), Is.EqualTo(new[] { "other" }));
    }

    [Test]
    public void IsKeyTaken_CountsRemovedLinks()
    {
        _book.Add(LinkKind.HandsOver, "research", "spike");
        _book.RemoveAllFor("spike");

        Assert.That(_book.IsKeyTaken("spike"), Is.True);
        Assert.That(_book.IsKeyTaken("never-linked"), Is.False);
    }

    [Test]
    public void Links_SurviveAReopen()
    {
        var link = _book.Add(LinkKind.HandsOver, "a", "b", output: @"C:\work\REPORT.md").Link!;
        _book.Satisfy(link.Id, "checked");

        var reopened = Open();

        Assert.That(reopened.Active.Single(), Is.EqualTo(_book.Active.Single()));
    }

    [Test]
    public void Open_TreatsAnUnreadableFileAsEmpty()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "links.json"), "{ not json");

        Assert.That(Open().All, Is.Empty);
    }

    [Test]
    public void Open_ReadsSpawnedAndFeedsLinksAsHandOversAndReportBacks()
    {
        WriteLegacy(
            Legacy("lk-1", "Spawned", "parent", "child"),
            Legacy("lk-2", "Spawned", "child", "grandchild", removed: "2026-10-22T12:07:54+00:00"),
            Legacy("lk-3", "Feeds", "child", "grandchild", output: @"C:\\work\\REPORT.md", satisfied: "2026-10-22T12:09:47+00:00"),
            Legacy("lk-4", "Spawned", "research", "spike"),
            Legacy("lk-5", "Feeds", "spike", "research", output: @"C:\\work\\SPIKE.md"));

        var book = Open();

        Assert.That(book.Active.Select(link => (link.Id, link.Kind)), Is.EquivalentTo(new[]
        {
            ("lk-1", LinkKind.HandsOver),
            ("lk-3", LinkKind.HandsOver),
            ("lk-4", LinkKind.HandsOver),
            ("lk-5", LinkKind.ReportsBack),
        }));
        Assert.That(book.Active.Single(link => link.Id == "lk-3").SatisfiedUtc, Is.Not.Null);
    }

    [Test]
    public void Open_KeepsTheFeedsHalfWhereOnePairStoodJoinedBothWays()
    {
        WriteLegacy(
            Legacy("lk-1", "Spawned", "design", "implementation", created: "2026-10-22T08:00:00+00:00"),
            Legacy("lk-2", "Feeds", "design", "implementation", output: @"C:\\work\\DESIGN.md", created: "2026-10-22T09:00:00+00:00"));

        var book = Open();

        Assert.That(book.Active.Single().Id, Is.EqualTo("lk-2"));
        Assert.That(book.Active.Single().Output, Is.EqualTo(@"C:\work\DESIGN.md"));
        Assert.That(book.All.Single(link => link.Id == "lk-1").RemovedUtc, Is.EqualTo(new DateTimeOffset(2026, 10, 22, 9, 0, 0, TimeSpan.Zero)));
    }

    private void WriteLegacy(params string[] links)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "links.json"), "[" + string.Join(",", links) + "]");
    }

    private static string Legacy(string id, string kind, string from, string to, string? output = null,
        string? satisfied = null, string? removed = null, string created = "2026-10-22T08:00:00+00:00") =>
        $$"""
          {"id":"{{id}}","kind":"{{kind}}","from":"{{from}}","to":"{{to}}","createdUtc":"{{created}}","createdBy":"board",
           "brief":null,"handOver":null,"output":{{Json(output)}},"satisfiedUtc":{{Json(satisfied)}},
           "satisfiedBy":{{Json(satisfied is null ? null : "by hand: ok")}},"removedUtc":{{Json(removed)}}}
          """;

    private static string Json(string? value) => value is null ? "null" : $"\"{value}\"";

    private LinkBook Open() => new(new LinkStateStore(Path.Combine(_directory, "links.json")), _clock);

    private sealed class MovableClock(DateTimeOffset start) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = start;

        public void Advance(TimeSpan span) => UtcNow += span;
    }
}
