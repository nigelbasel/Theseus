using Theseus.Core.Abstractions;
using Theseus.Core.Proposals;

namespace Theseus.Tests.Proposals;

[TestFixture]
public class ProposalBookTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);
    private static readonly ProposalCheckResult Passed = new(null, []);

    private string _directory = string.Empty;

    [SetUp]
    public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), $"theseus-proposals-{Guid.NewGuid():N}");

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void Propose_TheSameTitleTwiceFromOneWorkstreamGivesOneProposal()
    {
        var book = Book();

        var first = book.Propose("auth-research", "s1", Request("Cache spike"), Passed);
        var second = book.Propose("auth-research", "s2", Request("  cache   SPIKE ") with { Reason = "Updated." }, Passed);

        Assert.That(book.Pending, Has.Count.EqualTo(1));
        Assert.That(second.IsRepeat, Is.True);
        Assert.That(second.Proposal!.Id, Is.EqualTo(first.Proposal!.Id));
        Assert.That(book.Pending[0].Request.Reason, Is.EqualTo("Updated."));
        Assert.That(book.Pending[0].SessionId, Is.EqualTo("s2"));
    }

    [Test]
    public void Propose_RefusesTheFourthPendingProposalFromOneWorkstream()
    {
        var book = Book();

        foreach (var title in new[] { "One", "Two", "Three" })
        {
            Assert.That(book.Propose("auth-research", "s1", Request(title), Passed).Refusal, Is.Null);
        }

        var fourth = book.Propose("auth-research", "s1", Request("Four"), Passed);

        Assert.That(fourth.Refusal, Does.Contain("3 proposals from this workstream are already waiting"));
        Assert.That(book.Propose("token-spike", "s9", Request("Four"), Passed).Refusal, Is.Null, "the limit is per workstream");
    }

    [Test]
    public void Propose_ADecidedProposalFreesItsPlace()
    {
        var book = Book();
        var ids = new[] { "One", "Two", "Three" }.Select(title => book.Propose("auth-research", "s1", Request(title), Passed).Proposal!.Id).ToList();

        book.Reject(ids[0], "Not now.");

        Assert.That(book.Propose("auth-research", "s1", Request("Four"), Passed).Refusal, Is.Null);
    }

    [Test]
    public void Propose_RefusesWhatTheCheckRefused()
    {
        var book = Book();

        var outcome = book.Propose("auth-research", "s1", Request("Cache spike"), ProposalCheckResult.Refused("No brief."));

        Assert.That(outcome.Refusal, Is.EqualTo("No brief."));
        Assert.That(book.All, Is.Empty);
    }

    [Test]
    public void Withdraw_WorksOnlyOnTheCallersOwnPendingProposal()
    {
        var book = Book();
        var id = book.Propose("auth-research", "s1", Request("Cache spike"), Passed).Proposal!.Id;

        Assert.That(book.Withdraw("token-spike", "s9", id).Refusal, Does.Contain("another workstream"));

        var withdrawn = book.Withdraw("auth-research", "s1", id);

        Assert.That(withdrawn.Proposal!.Status, Is.EqualTo(ProposalStatus.Withdrawn));
        Assert.That(book.Withdraw("auth-research", "s1", id).Refusal, Does.Contain("already withdrawn"));
    }

    [Test]
    public void Approve_RecordsTheCardItCreated()
    {
        var book = Book();
        var id = book.Propose("auth-research", "s1", Request("Cache spike"), Passed).Proposal!.Id;

        book.Approve(id, "cache-spike");

        Assert.That(book.Find(id)!.Status, Is.EqualTo(ProposalStatus.Approved));
        Assert.That(book.Find(id)!.ResultingKey, Is.EqualTo("cache-spike"));
        Assert.That(book.Pending, Is.Empty);
        Assert.That(book.Reject(id, null).Refusal, Does.Contain("already approved"));
    }

    [Test]
    public void Reject_KeepsNigelsNoteForTheProposer()
    {
        var book = Book();
        var id = book.Propose("auth-research", "s1", Request("Cache spike"), Passed).Proposal!.Id;

        book.Reject(id, "  Fold it into the research.  ");

        Assert.That(book.From("auth-research")[0].DecisionNote, Is.EqualTo("Fold it into the research."));
    }

    [Test]
    public void AProposalSurvivesARestart()
    {
        var id = Book().Propose("auth-research", "s1", Request("Cache spike"), new ProposalCheckResult(null, ["Looks like X."]))
            .Proposal!.Id;

        var reopened = Book();

        Assert.That(reopened.Pending.Single().Id, Is.EqualTo(id));
        Assert.That(reopened.Pending.Single().Request.Brief, Is.EqualTo(@"C:\work\proposals\cache-spike\BRIEF.md"));
        Assert.That(reopened.Pending.Single().Warnings, Is.EqualTo(new[] { "Looks like X." }));
    }

    [Test]
    public void Changed_IsRaisedForEachChangeButNotForARefusal()
    {
        var book = Book();
        var raised = 0;
        book.Changed += (_, _) => raised++;

        var id = book.Propose("auth-research", "s1", Request("Cache spike"), Passed).Proposal!.Id;
        book.Withdraw("token-spike", "s9", id);
        book.Reject(id, null);

        Assert.That(raised, Is.EqualTo(2));
    }

    private ProposalBook Book() =>
        new(new ProposalStateStore(Path.Combine(_directory, "proposals.json")), new FixedClock(Nine));

    private static ProposalRequest Request(string title) =>
        new(title, @"C:\work\proposals\cache-spike\BRIEF.md", true, null, "A verdict.", [], null);

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
