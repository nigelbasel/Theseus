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
        var change = _book.Add(LinkKind.Feeds, "token-spike", "auth-implementation", output: @"C:\work\REPORT.md");

        Assert.That(change.Refusal, Is.Null);
        Assert.That(change.Link!.Kind, Is.EqualTo(LinkKind.Feeds));
        Assert.That(change.Link.From, Is.EqualTo("token-spike"));
        Assert.That(change.Link.To, Is.EqualTo("auth-implementation"));
        Assert.That(change.Link.Output, Is.EqualTo(@"C:\work\REPORT.md"));
        Assert.That(change.Link.CreatedUtc, Is.EqualTo(Nine));
        Assert.That(change.Link.CreatedBy, Is.EqualTo(Link.MadeOnTheBoard));
        Assert.That(_book.Active, Has.Count.EqualTo(1));
    }

    [Test]
    public void Add_RefusesALinkFromACardToItself()
    {
        var change = _book.Add(LinkKind.Feeds, "auth-research", "auth-research");

        Assert.That(change.Link, Is.Null);
        Assert.That(change.Refusal, Does.Contain("itself"));
    }

    [Test]
    public void Add_RefusesTheSameLinkTwice()
    {
        _book.Add(LinkKind.Feeds, "token-spike", "auth-implementation");

        var change = _book.Add(LinkKind.Feeds, "token-spike", "auth-implementation");

        Assert.That(change.Refusal, Does.Contain("already"));
        Assert.That(_book.Active, Has.Count.EqualTo(1));
    }

    [Test]
    public void Add_RefusesAFeedsCycleOfTwo()
    {
        _book.Add(LinkKind.Feeds, "a", "b");

        var change = _book.Add(LinkKind.Feeds, "b", "a");

        Assert.That(change.Refusal, Does.Contain("cycle"));
    }

    [Test]
    public void Add_RefusesAFeedsCycleOfThree()
    {
        _book.Add(LinkKind.Feeds, "a", "b");
        _book.Add(LinkKind.Feeds, "b", "c");

        var change = _book.Add(LinkKind.Feeds, "c", "a");

        Assert.That(change.Refusal, Does.Contain("cycle"));
    }

    [Test]
    public void Add_AcceptsADiamond()
    {
        _book.Add(LinkKind.Feeds, "research", "spike-one");
        _book.Add(LinkKind.Feeds, "research", "spike-two");
        _book.Add(LinkKind.Feeds, "spike-one", "implementation");

        var change = _book.Add(LinkKind.Feeds, "spike-two", "implementation");

        Assert.That(change.Refusal, Is.Null);
    }

    [Test]
    public void Add_AcceptsAReportBackBesideTheSpawnedLink()
    {
        _book.Add(LinkKind.Spawned, "research", "spike");

        var change = _book.Add(LinkKind.Feeds, "spike", "research");

        Assert.That(change.Refusal, Is.Null);
    }

    [Test]
    public void Add_RefusesASpawnedLinkThatMakesACardItsOwnAncestor()
    {
        _book.Add(LinkKind.Spawned, "research", "spike");
        _book.Add(LinkKind.Spawned, "spike", "implementation");

        var change = _book.Add(LinkKind.Spawned, "implementation", "research");

        Assert.That(change.Refusal, Does.Contain("ancestor"));
    }

    [Test]
    public void Add_RefusesASecondParent()
    {
        _book.Add(LinkKind.Spawned, "research", "spike");

        var change = _book.Add(LinkKind.Spawned, "other-research", "spike");

        Assert.That(change.Refusal, Does.Contain("research"));
    }

    [Test]
    public void Add_IgnoresRemovedLinksWhenLookingForCycles()
    {
        var first = _book.Add(LinkKind.Feeds, "a", "b").Link!;
        _book.Remove(first.Id);

        var change = _book.Add(LinkKind.Feeds, "b", "a");

        Assert.That(change.Refusal, Is.Null);
    }

    [Test]
    public void Remove_KeepsTheLinkAsATombstone()
    {
        var link = _book.Add(LinkKind.Feeds, "a", "b").Link!;
        _clock.Advance(TimeSpan.FromMinutes(5));

        _book.Remove(link.Id);

        Assert.That(_book.Active, Is.Empty);
        Assert.That(_book.All.Single().RemovedUtc, Is.EqualTo(Nine.AddMinutes(5)));
    }

    [Test]
    public void Satisfy_RecordsTheReasonGivenByHand()
    {
        var link = _book.Add(LinkKind.Feeds, "a", "b").Link!;
        _clock.Advance(TimeSpan.FromMinutes(5));

        var change = _book.Satisfy(link.Id, "Read the spike's handoff");

        Assert.That(change.Link!.SatisfiedUtc, Is.EqualTo(Nine.AddMinutes(5)));
        Assert.That(change.Link.SatisfiedBy, Is.EqualTo("by hand: Read the spike's handoff"));
    }

    [Test]
    public void Satisfy_RefusesWithoutAReason()
    {
        var link = _book.Add(LinkKind.Feeds, "a", "b").Link!;

        var change = _book.Satisfy(link.Id, "  ");

        Assert.That(change.Refusal, Does.Contain("reason"));
        Assert.That(_book.Active.Single().SatisfiedUtc, Is.Null);
    }

    [Test]
    public void Satisfy_RefusesASpawnedLink()
    {
        var link = _book.Add(LinkKind.Spawned, "a", "b").Link!;

        var change = _book.Satisfy(link.Id, "done");

        Assert.That(change.Refusal, Is.Not.Null);
    }

    [Test]
    public void RemoveAllFor_TombstonesEveryLinkTouchingADeletedCard()
    {
        _book.Add(LinkKind.Spawned, "research", "spike");
        _book.Add(LinkKind.Feeds, "spike", "research");
        _book.Add(LinkKind.Feeds, "other", "elsewhere");

        _book.RemoveAllFor("spike");

        Assert.That(_book.Active.Select(link => link.From), Is.EqualTo(new[] { "other" }));
    }

    [Test]
    public void IsKeyTaken_CountsRemovedLinks()
    {
        _book.Add(LinkKind.Spawned, "research", "spike");
        _book.RemoveAllFor("spike");

        Assert.That(_book.IsKeyTaken("spike"), Is.True);
        Assert.That(_book.IsKeyTaken("never-linked"), Is.False);
    }

    [Test]
    public void Links_SurviveAReopen()
    {
        var link = _book.Add(LinkKind.Feeds, "a", "b", output: @"C:\work\REPORT.md").Link!;
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

    private LinkBook Open() => new(new LinkStateStore(Path.Combine(_directory, "links.json")), _clock);

    private sealed class MovableClock(DateTimeOffset start) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = start;

        public void Advance(TimeSpan span) => UtcNow += span;
    }
}
