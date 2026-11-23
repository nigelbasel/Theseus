using Theseus.App.ViewModels;
using Theseus.Core.Proposals;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The one proposal window (sessions-run-the-project 0.1, 0.2, 0.6): what is proposed, read-only; Approve makes the card,
/// Reject… records the note, Cancel decides nothing.
/// </summary>
[TestFixture]
public class ProposalWindowViewModelTests
{
    private readonly List<string> _ran = [];
    private string? _block;
    private string? _approveRefusal;

    [SetUp]
    public void SetUp()
    {
        _ran.Clear();
        _block = null;
        _approveRefusal = null;
    }

    [Test]
    public void ItShowsWhatTheSessionProposed()
    {
        var model = Model();

        Assert.Multiple(() =>
        {
            Assert.That(model.Title, Is.EqualTo("Proposal: Cache spike"));
            Assert.That(model.Heading, Is.EqualTo("Auth research proposes Cache spike"));
            Assert.That(model.Objective, Is.EqualTo("Under objective 1.6"));
            Assert.That(model.Where, Is.EqualTo("Records: cache-spike · Branch: nigel/cache-spike · Repos: Theseus"));
            Assert.That(model.Detail, Is.EqualTo("Reports back to Auth research when done. “Too slow”"));
            Assert.That(model.DoneCriteria, Is.EqualTo("Done when: A verdict."));
            Assert.That(model.JiraKeys, Is.EqualTo("Jira: NUF-1"));
            Assert.That(model.Warnings, Is.EqualTo("⚠ Looks like Token spike."));
            Assert.That(model.Brief, Is.EqualTo("# Brief\n"));
            Assert.That(model.Block, Is.Empty);
        });
    }

    [Test]
    public void AnAdHocProposalNamingNothing_SaysSo()
    {
        var model = Model(new ProposalRequest("Cache spike", "brief.md", false, null, "A verdict.", [], null));

        Assert.Multiple(() =>
        {
            Assert.That(model.Objective, Is.EqualTo("Ad hoc: under no objective"));
            Assert.That(model.Where, Is.EqualTo("Records: not named · Branch: none · Repos: read-only"));
            Assert.That(model.Detail, Is.EqualTo("Carries Auth research's work on."));
            Assert.That(model.JiraKeys, Is.Empty);
        });
    }

    [Test]
    public void Approve_MakesTheCard_AndCloses()
    {
        var model = Model();
        var closed = false;
        model.Closing += (_, _) => closed = true;

        model.ApproveCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_ran, Is.EqualTo(new[] { "Approve:pr-1" }));
            Assert.That(closed, Is.True);
        });
    }

    [Test]
    public void ARefusedApproval_StaysOpen_AndSaysWhy()
    {
        _approveRefusal = "It cannot be approved yet: the file changed.";
        var model = Model();
        var closed = false;
        model.Closing += (_, _) => closed = true;

        model.ApproveCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(closed, Is.False);
            Assert.That(model.Notice, Is.EqualTo("It cannot be approved yet: the file changed."));
        });
    }

    [Test]
    public void AProposalThatCannotBeApproved_HasApproveDisabled_WithTheReason()
    {
        _block = "it names no objective of theseus";
        var model = Model();

        Assert.Multiple(() =>
        {
            Assert.That(model.ApproveCommand.CanExecute(null), Is.False);
            Assert.That(model.Block, Is.EqualTo("Cannot be approved yet: it names no objective of theseus"));
        });
    }

    [Test]
    public void Reject_AsksForANote_ThenRejectsWithIt_AndCloses()
    {
        var model = Model();
        var closed = false;
        model.Closing += (_, _) => closed = true;

        Assert.That(model.RejectCommand.CanExecute(null), Is.False, "Reject… opens the note first");

        model.AskRejectCommand.Execute(null);
        model.Note = "  Not now.  ";

        Assert.That(model.ApproveCommand.CanExecute(null), Is.False, "no approving with the note box open");

        model.RejectCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(_ran, Is.EqualTo(new[] { "Reject:pr-1:Not now." }));
            Assert.That(closed, Is.True);
        });
    }

    [Test]
    public void ABlankNote_RejectsWithNone()
    {
        var model = Model();
        model.AskRejectCommand.Execute(null);

        model.RejectCommand.Execute(null);

        Assert.That(_ran, Is.EqualTo(new[] { "Reject:pr-1:" }));
    }

    [Test]
    public void Cancel_InARejection_HidesTheNote_AndOtherwiseClosesWithNothingDecided()
    {
        var model = Model();
        var closed = 0;
        model.Closing += (_, _) => closed++;
        model.AskRejectCommand.Execute(null);
        model.Note = "Not now.";

        model.CancelCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.IsRejecting, Is.False);
            Assert.That(model.Note, Is.Empty);
            Assert.That(closed, Is.EqualTo(0));
        });

        model.CancelCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(closed, Is.EqualTo(1));
            Assert.That(_ran, Is.Empty);
        });
    }

    private ProposalWindowViewModel Model(ProposalRequest? request = null) =>
        new(new WorkstreamProposal("pr-1", ProposalStatus.Pending, "auth-research", "s",
                request ?? new ProposalRequest("Cache spike", "brief.md", true, null, "A verdict.", ["NUF-1"], "Too slow")
                {
                    Objective = "1.6", Slug = "cache-spike", Branch = "nigel/cache-spike", Repos = ["Theseus"],
                },
                ["Looks like Token spike."], DateTimeOffset.UnixEpoch),
            "Auth research", "# Brief\n",
            _ => _block,
            proposal =>
            {
                _ran.Add($"Approve:{proposal.Id}");

                return _approveRefusal;
            },
            (proposal, note) =>
            {
                _ran.Add($"Reject:{proposal.Id}:{note}");

                return null;
            });
}
