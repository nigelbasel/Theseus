using Theseus.Core.Abstractions;
using Theseus.Core.Links;

namespace Theseus.Tests.Links;

[TestFixture]
public class LinkBookReportTests
{
    private const string Output = @"C:\work\proposals\token-spike\REPORT.md";
    private static readonly DateTimeOffset Nine = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private LinkBook _book = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-reports-{Guid.NewGuid():N}");
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
    public void Report_OnTheOneLinkThatWaitsIsRecordedAndLeavesItUnsatisfied()
    {
        var link = Feeds("token-spike", "auth-research");

        var outcome = _book.Report("token-spike", "session-1", [], Output, "Found the lifetime.", "go");

        Assert.That(outcome.Refusal, Is.Null);
        var stored = _book.Active.Single(each => each.Id == link.Id);
        Assert.That(stored.LatestReport, Is.EqualTo(new LinkReport(Nine, "session-1", Output, "Found the lifetime.", "go")));
        Assert.That(stored.SatisfiedUtc, Is.Null);
    }

    [Test]
    public void Report_IsRefusedWhenTheCallerIsNotTheUpstream()
    {
        var link = Feeds("token-spike", "auth-research");

        var outcome = _book.Report("auth-research", "session-1", [link.Id], Output, "Done.", null);

        Assert.That(outcome.Refusal, Does.Contain("not the upstream"));
        Assert.That(_book.Active.Single().LatestReport, Is.Null);
    }

    [Test]
    public void Report_WithNoLinksNamedIsRefusedWhenItHasNoOutputs()
    {
        Feeds("token-spike", "auth-research");

        Assert.That(_book.Report("auth-research", "session-1", [], Output, "Done.", null).Refusal,
            Does.Contain("No card waits"));
    }

    [Test]
    public void Report_IsRefusedWhenTheDocumentIsNotTheLinksOutput()
    {
        Feeds("token-spike", "auth-research");

        var outcome = _book.Report("token-spike", "session-1", [], @"C:\work\HANDOFF-token-spike.md", "Done.", null);

        Assert.That(outcome.Refusal, Does.Contain(Output));
        Assert.That(_book.Active.Single().LatestReport, Is.Null);
    }

    [Test]
    public void Report_MatchesTheOutputWhateverItsCase()
    {
        Feeds("token-spike", "auth-research");

        Assert.That(_book.Report("token-spike", "session-1", [], Output.ToUpperInvariant(), "Done.", null).Refusal, Is.Null);
    }

    [Test]
    public void Report_IsRefusedWhenSeveralLinksWaitAndNoneIsNamed()
    {
        Feeds("token-spike", "auth-research");
        Feeds("token-spike", "auth-implementation");

        var outcome = _book.Report("token-spike", "session-1", [], Output, "Done.", null);

        Assert.That(outcome.Refusal, Does.Contain("More than one"));
        Assert.That(_book.Active.All(link => link.LatestReport is null), Is.True);
    }

    [Test]
    public void Report_OnLinksNamedReachesEachOfThem()
    {
        var one = Feeds("token-spike", "auth-research");
        var two = Feeds("token-spike", "auth-implementation");

        var outcome = _book.Report("token-spike", "session-1", [one.Id, two.Id], Output, "Done.", "done");

        Assert.That(outcome.Links.Select(link => link.Id), Is.EquivalentTo(new[] { one.Id, two.Id }));
        Assert.That(_book.Active.All(link => link.LatestReport?.Verdict == "done"), Is.True);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Report_NeedsASummary(string summary)
    {
        Feeds("token-spike", "auth-research");

        Assert.That(_book.Report("token-spike", "session-1", [], Output, summary, null).Refusal, Does.Contain("summary"));
    }

    [Test]
    public void Report_RefusesASummaryLongerThanTheLimit()
    {
        Feeds("token-spike", "auth-research");

        var summary = new string('x', LinkReport.MaxSummaryLength + 1);

        Assert.That(_book.Report("token-spike", "session-1", [], Output, summary, null).Refusal, Does.Contain("2000"));
    }

    [Test]
    public void Report_RefusesAVerdictThatIsNotOneOfTheFive()
    {
        Feeds("token-spike", "auth-research");

        Assert.That(_book.Report("token-spike", "session-1", [], Output, "Done.", "ship it").Refusal, Does.Contain("verdict"));
    }

    [Test]
    public void Report_ReplacesTheEarlierReport()
    {
        Feeds("token-spike", "auth-research");

        _book.Report("token-spike", "session-1", [], Output, "First.", "partial");
        _book.Report("token-spike", "session-2", [], Output, "Second.", "go");

        Assert.That(_book.Active.Single().LatestReport!.Summary, Is.EqualTo("Second."));
    }

    [Test]
    public void Report_SurvivesReopeningTheBook()
    {
        Feeds("token-spike", "auth-research");
        _book.Report("token-spike", "session-1", [], Output, "Found it.", "go");

        Assert.That(Open().Active.Single().LatestReport!.Summary, Is.EqualTo("Found it."));
    }

    [Test]
    public void Confirm_SatisfiesAReportedLink()
    {
        var link = Feeds("token-spike", "auth-research");
        _book.Report("token-spike", "session-1", [], Output, "Found it.", "go");

        var change = _book.Confirm(link.Id);

        Assert.That(change.Refusal, Is.Null);
        Assert.That(change.Link!.SatisfiedBy, Is.EqualTo(LinkBook.ConfirmedReport));
        Assert.That(change.Link.SatisfiedUtc, Is.EqualTo(Nine));
    }

    [Test]
    public void Confirm_IsRefusedWithNoReport()
    {
        var link = Feeds("token-spike", "auth-research");

        Assert.That(_book.Confirm(link.Id).Refusal, Does.Contain("no report"));
    }

    [Test]
    public void Changed_IsRaisedForAReportAndNotForARefusal()
    {
        Feeds("token-spike", "auth-research");
        var raised = 0;
        _book.Changed += (_, _) => raised++;

        _book.Report("auth-research", "session-1", [], Output, "Wrong end.", null);
        _book.Report("token-spike", "session-1", [], Output, "Found it.", null);

        Assert.That(raised, Is.EqualTo(1));
    }

    [Test]
    public void Report_FromManyThreadsAtOnceLosesNothing()
    {
        var links = Enumerable.Range(0, 20).Select(index => Feeds($"spike-{index}", "auth-research")).ToList();

        Parallel.ForEach(links, link => _book.Report(link.From, "session-1", [], Output, "Done.", null));

        Assert.That(_book.Active.Count(link => link.LatestReport is not null), Is.EqualTo(20));
        Assert.That(Open().Active.Count(link => link.LatestReport is not null), Is.EqualTo(20));
    }

    private Link Feeds(string from, string to) => _book.Add(LinkKind.Feeds, from, to, output: Output).Link!;

    private LinkBook Open() => new(new LinkStateStore(Path.Combine(_directory, "links.json")), new FixedClock(Nine));

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
