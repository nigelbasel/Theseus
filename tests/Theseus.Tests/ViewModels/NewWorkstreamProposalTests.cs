using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;
using Theseus.Core.Colour;
using Theseus.Core.Domain;
using Theseus.Core.Proposals;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Approving a proposal makes its card without the composer (sessions-run-the-project item 0.2): the plan Create would
/// write, from what the session asked for, and nothing typed in an open composer is touched.
/// </summary>
[TestFixture]
public class NewWorkstreamProposalTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void CreateFromProposal_MakesTheFollowOnTheProposalAsksFor()
    {
        var composer = Composer(out var created, out var plans);

        var approval = composer.CreateFromProposal(Parent(), Proposal(), startNow: false);

        var made = created.Single();
        var plan = plans.TryRead(made.Key)!;

        Assert.Multiple(() =>
        {
            Assert.That(approval.Refusal, Is.Null);
            Assert.That(approval.Key, Is.EqualTo(made.Key));
            Assert.That(made.ProposalId, Is.EqualTo("pr-1"));
            Assert.That(made.ParentKey, Is.EqualTo("auth-research"));
            Assert.That(made.StartNow, Is.False, "an approved card starts only when someone starts it");
            Assert.That(made.Stage, Is.EqualTo("New"));
            Assert.That(made.Area, Is.EqualTo("Research"));
            Assert.That(made.ReportTo, Is.EqualTo(Path.Combine(Path.GetTempPath(), "proposals", "cache-spike", "REPORT.md")));
            Assert.That(plan.Title, Is.EqualTo("Cache spike"));
            Assert.That(plan.Cwd, Is.EqualTo(Path.GetTempPath()));
            Assert.That(plan.ResumeSessionId, Is.Null);
            Assert.That(plan.Prompt, Does.Contain(@"Read your brief first: C:\work\proposals\cache-spike\BRIEF.md"));
        });
    }

    [Test]
    public void CreateFromProposal_UsesTheReportPathTheProposalNamed()
    {
        var composer = Composer(out var created, out _);
        var proposal = Proposal();

        composer.CreateFromProposal(Parent(), proposal with { Request = proposal.Request with { ExpectedOutput = @"C:\work\out\VERDICT.md" } }, false);

        Assert.That(created.Single().ReportTo, Is.EqualTo(@"C:\work\out\VERDICT.md"));
    }

    [Test]
    public void CreateFromProposal_ThatDoesNotReportBackHasNoReportPath()
    {
        var composer = Composer(out var created, out _);
        var proposal = Proposal();

        composer.CreateFromProposal(Parent(), proposal with { Request = proposal.Request with { ReportBack = false } }, true);

        Assert.Multiple(() =>
        {
            Assert.That(created.Single().ReportTo, Is.Null);
            Assert.That(created.Single().StartNow, Is.True);
        });
    }

    [Test]
    public void CreateFromProposal_LeavesAnOpenComposerAlone()
    {
        var composer = Composer(out var created, out _);
        composer.OpenCommand.Execute(null);
        composer.Title = "Something else";
        composer.Prompt = "Do it.";

        composer.CreateFromProposal(Parent(), Proposal(), false);

        Assert.Multiple(() =>
        {
            Assert.That(composer.IsOpen, Is.True);
            Assert.That(composer.Title, Is.EqualTo("Something else"));
            Assert.That(composer.Prompt, Is.EqualTo("Do it."));
            Assert.That(created.Single().ProposalId, Is.EqualTo("pr-1"));
        });
    }

    [Test]
    public void CreateFromProposal_RefusesWhenTheProposersFolderIsGone()
    {
        var composer = Composer(out var created, out _);
        var parent = Parent() with { WorkingDirectory = Path.Combine(Path.GetTempPath(), "no-such-folder-" + Guid.NewGuid().ToString("N")) };

        var approval = composer.CreateFromProposal(parent, Proposal(), false);

        Assert.Multiple(() =>
        {
            Assert.That(approval.Refusal, Does.Contain("is not there"));
            Assert.That(created, Is.Empty);
        });
    }

    [Test]
    public void AnOrdinaryNewWorkstreamApprovesNothing()
    {
        var composer = Composer(out var created, out _);

        composer.OpenCommand.Execute(null);
        composer.Title = "Something else";
        composer.WorkingDirectory = Path.GetTempPath();
        composer.Prompt = "Do it.";
        composer.CreateCommand.Execute(null);

        Assert.That(created.Single().ProposalId, Is.Null);
    }

    [Test]
    public void ProposalPrompt_SaysWhatApprovalDidNotAuthorise()
    {
        var prompt = ProposalPrompt.For(Proposal().Request with { ReportBack = false });

        Assert.That(prompt, Does.Contain("Done when: A verdict on dropping the token cache."));
        Assert.That(prompt, Does.Contain("carry this work on"));
        Assert.That(prompt, Does.Contain("no Jira item, branch, worktree, push or pull request"));
    }

    private static Workstream Parent() =>
        new("auth-research", "Auth research", "#208DD7", false, Path.GetTempPath(), [], "Carry on.", 0, "New", false, false,
            false, null, [], null, Nine) { Area = "Research" };

    private static WorkstreamProposal Proposal() =>
        new("pr-1", ProposalStatus.Pending, "auth-research", "research-session",
            new ProposalRequest("Cache spike", @"C:\work\proposals\cache-spike\BRIEF.md", true, null,
                "A verdict on dropping the token cache.", [], null),
            ["Looks like Token spike."], Nine);

    private static NewWorkstreamViewModel Composer(out List<NewWorkstream> created, out FakePlans plans)
    {
        var made = new List<NewWorkstream>();
        var store = new FakePlans();

        created = made;
        plans = store;

        return new NewWorkstreamViewModel(store, new FakeColours(), () => ["New", "Doing"], () => ["Ideas", "Research"], made.Add);
    }

    private sealed class FakePlans : IPlanStore
    {
        private readonly Dictionary<string, SessionPlan> _plans = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> Keys() => [.. _plans.Keys];

        public string PathFor(string key) => $@"C:\plans\{key}.json";

        public SessionPlan? TryRead(string key) => _plans.GetValueOrDefault(key);

        public void Write(string key, SessionPlan plan) => _plans[key] = plan;

        public void Delete(string key) => _plans.Remove(key);
    }

    private sealed class FakeColours : ITabColourAllocator
    {
        public string Allocate(ColourAllocationState state) => "#208DD7";
    }
}
