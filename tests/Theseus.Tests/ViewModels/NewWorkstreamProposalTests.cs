using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;
using Theseus.Core.Colour;
using Theseus.Core.Domain;
using Theseus.Core.Proposals;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Approving a proposal is creating its card in the composer, filled in from what the session asked
/// for. Closing the composer approves nothing.
/// </summary>
[TestFixture]
public class NewWorkstreamProposalTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void OpenProposal_FillsTheComposerAsAFollowOnFromTheProposer()
    {
        var composer = Composer(out _, out _);

        composer.OpenProposal(Parent(), Proposal());

        Assert.Multiple(() =>
        {
            Assert.That(composer.IsFollowOn, Is.True);
            Assert.That(composer.IsProposal, Is.True);
            Assert.That(composer.Title, Is.EqualTo("Cache spike"));
            Assert.That(composer.WorkingDirectory, Is.EqualTo(Path.GetTempPath()));
            Assert.That(composer.ReportBack, Is.True);
            Assert.That(composer.Prompt, Does.Contain(@"Read your brief first: C:\work\proposals\cache-spike\BRIEF.md"));
            Assert.That(composer.StartNow, Is.False, "an approved card starts only when Nigel says");
            Assert.That(composer.ProposalWarnings, Does.Contain("Looks like Token spike."));
        });
    }

    [Test]
    public void Create_ApprovesTheProposalWithTheCardItMade()
    {
        var composer = Composer(out var created, out var plans);

        composer.OpenProposal(Parent(), Proposal());
        composer.CreateCommand.Execute(null);

        var made = created.Single();

        Assert.That(made.ProposalId, Is.EqualTo("pr-1"));
        Assert.That(made.ParentKey, Is.EqualTo("auth-research"));
        Assert.That(made.ReportTo, Is.EqualTo(Path.Combine(Path.GetTempPath(), "proposals", "cache-spike", "REPORT.md")));
        Assert.That(plans.TryRead(made.Key)!.ResumeSessionId, Is.Null);
        Assert.That(plans.TryRead(made.Key)!.Cwd, Is.EqualTo(Path.GetTempPath()));
    }

    [Test]
    public void Create_UsesTheReportPathTheProposalNamed()
    {
        var composer = Composer(out var created, out _);
        var proposal = Proposal();

        composer.OpenProposal(Parent(), proposal with { Request = proposal.Request with { ExpectedOutput = @"C:\work\out\VERDICT.md" } });
        composer.CreateCommand.Execute(null);

        Assert.That(created.Single().ReportTo, Is.EqualTo(@"C:\work\out\VERDICT.md"));
    }

    [Test]
    public void AnOrdinaryNewWorkstreamAfterAProposalApprovesNothing()
    {
        var composer = Composer(out var created, out _);

        composer.OpenProposal(Parent(), Proposal());
        composer.CloseCommand.Execute(null);
        composer.OpenCommand.Execute(null);
        composer.Title = "Something else";
        composer.WorkingDirectory = Path.GetTempPath();
        composer.Prompt = "Do it.";
        composer.CreateCommand.Execute(null);

        Assert.That(created.Single().ProposalId, Is.Null);
        Assert.That(composer.IsProposal, Is.False);
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
            false, null, [], null, Nine);

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

        return new NewWorkstreamViewModel(store, new FakeColours(), () => ["New"], () => ["Research"], made.Add);
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
