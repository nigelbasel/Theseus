using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Core.Proposals;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Open brief shows the brief in Theseus's own Markdown view, not the system's editor for .md files (Nigel, 2026-11-22).
/// </summary>
[TestFixture]
public class ProposalBriefTests
{
    private string _brief = null!;

    [SetUp]
    public void SetUp()
    {
        _brief = Path.Combine(Path.GetTempPath(), $"theseus-brief-{Guid.NewGuid():N}.md");
        File.WriteAllText(_brief, "# Brief\n\nDo the thing.");
    }

    [TearDown]
    public void TearDown() => File.Delete(_brief);

    [Test]
    public void OpenBrief_ShowsTheBriefInTheMarkdownView()
    {
        var proposal = new WorkstreamProposal("p1", ProposalStatus.Pending, "git-22", "s",
            new ProposalRequest("A follow-up", _brief, false, null, "it is built", [], null), [], DateTimeOffset.UnixEpoch);
        string? shown = null;
        var row = new ProposalRowViewModel(proposal, "git-22", "#2f80ed", _ => { }, (_, _) => null, path => shown = path);

        row.OpenBriefCommand.Execute(null);

        Assert.That(shown, Is.EqualTo(_brief));
    }

    [Test]
    public void TheMarkdownView_ShowsOneDocument_WithItsText()
    {
        var records = new RecordsViewModel("Brief: a follow-up", new RecordEntry("Brief", "What it asks", StageNodeState.Done, _brief), _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(records.Entries, Has.Count.EqualTo(1));
            Assert.That(records.Text, Does.Contain("Do the thing."));
            Assert.That(records.HasNone, Is.False);
        });
    }
}
