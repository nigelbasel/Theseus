using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Core.Proposals;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A project's lead card (Nigel, 2026-11-22): chosen or the first row's card, started by a project's Start with no run
/// state, shown in the view's header with Focus; and a pending proposal drawn under its proposer (1.1e item 30).
/// </summary>
[TestFixture]
public class ProjectLeadTests
{
    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void TheLead_IsTheFirstRowsCard_UnlessOneIsChosenThatARowStillNames()
    {
        var file = WriteBuildYourMarket(_folder).File!;

        Assert.That(ProjectLead.KeyOf(file, new Dictionary<string, string>()), Is.EqualTo(ServerSpec));
        Assert.That(ProjectLead.KeyOf(file, new Dictionary<string, string> { [BuildYourMarket] = "bym-mcp-w3-build-read-api" }),
            Is.EqualTo("bym-mcp-w3-build-read-api"));
        Assert.That(ProjectLead.KeyOf(file, new Dictionary<string, string> { [BuildYourMarket] = "gone" }), Is.EqualTo(ServerSpec),
            "a lead no row names any more falls back to the first row's card");
    }

    [Test]
    public void StartingAProjectWithNoRunState_StartsItsLead()
    {
        var pieces = BuildYourMarketPieces(_folder);

        var start = ProjectStart.For("BuildYourMarket", pieces, lead: "bym-mcp-w3-build-read-api");

        Assert.That(start.Entries.Select(entry => entry.Key), Is.EqualTo(new[] { "bym-mcp-w3-build-read-api" }));
        Assert.That(ProjectStart.For("BuildYourMarket", pieces, ["bym-mcp-host-x"], lead: ServerSpec).Entries.Select(entry => entry.Key),
            Is.EqualTo(new[] { "bym-mcp-host-x" }), "a run state still wins");
    }

    [Test]
    public void TheHeader_ShowsTheLeadAndItsState_WithFocusDisabledWhileItHasNoSession()
    {
        var idle = _views.Theseus();
        var running = _views.Theseus(SessionLifecycle.Running);

        Assert.That(idle.LeadText, Is.EqualTo("Lead: git-22 · idle"));
        Assert.That(idle.FocusLeadCommand!.CanExecute(null), Is.True);
        Assert.That(running.LeadText, Is.EqualTo("Lead: git-22 · busy"));

        running.FocusLeadCommand!.Execute(null);

        Assert.That(_views.Ran, Is.EqualTo(new[] { "Focus:git-22" }));
    }

    [Test]
    public void TheToolbarsFocusLeadIcon_SaysItsKeyAndWhatItDoes()
    {
        // G78: a hand-written tooltip includes the accelerator (git-22, 2026-11-24: the header's Focus was easy to miss).
        Assert.That(_views.Theseus().FocusLeadIconToolTip, Is.EqualTo("Focus lead session (Ctrl+L): bring git-22's terminal forward"));
        Assert.That(_views.One("feature").FocusLeadIconToolTip, Is.EqualTo("Focus lead session (Ctrl+L): a-card has no session: start it first"));
    }

    [Test]
    public void APendingProposal_IsANodeUnderItsProposersCurrentPiece()
    {
        var proposal = new WorkstreamProposal("p1", ProposalStatus.Pending, Git22, "s",
            new ProposalRequest("A follow-up", "brief.md", false, null, "it is built", [], null), [], DateTimeOffset.UnixEpoch);
        var row = new ProposalRowViewModel(proposal, "git-22", "#2f80ed", _ => { }, (_, _) => null);
        var actions = _views.Actions with { ProposalsOf = key => key == Git22 ? [row] : [] };
        var view = _views.Theseus(actions: actions);

        Assert.That(view.AllNodes.Where(node => node.HasProposals).Select(node => node.Name), Is.EqualTo(new[] { "projects-and-method-ui" }));
    }
}
