using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>What the owner's answer, done and reassignment write to project.md (git-22 for Nigel, 2026-11-24).</summary>
[TestFixture]
public class WaitEditsTests
{
    private const string Text = "# P\n\n**Kit:** method-v0.2\n**Primary branch:** owner/p\n\n## Phase 0: S\n\n### Objective 0.1: O\n"
                                + "- [ ] One.\n      Waiting: owner: name the agent; work: the audit store\n"
                                + "- [ ] Two.\n      Waiting: owner: name the agent\n"
                                + "- [ ] Three.\n      Waiting: owner: approve; on legal: approve\n\n"
                                + "**Pipeline:** freeform\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n";

    private static readonly WaitCriterion[] Both =
    [
        new("0.1", "O", 0, "One.", "name the agent") { Asked = "name the agent" },
        new("0.1", "O", 1, "Two.", "name the agent") { Asked = "name the agent" },
    ];

    private static IReadOnlyList<ProjectCriterion> Criteria(string text) => ProjectFileReader.Read("p", text).File!.Objectives[0].Criteria;

    [Test]
    public void Clear_TakesTheAskOff_AndKeepsTheCriterionsOtherAsks()
    {
        var criteria = Criteria(WaitEdits.Clear(Text, Both));

        Assert.That(criteria[0].Waiting!.Line, Is.EqualTo("Waiting: work: the audit store"));
        Assert.That(criteria[1].Waiting, Is.Null);
        Assert.That(criteria.Select(criterion => criterion.Text), Is.EqualTo(new[] { "One.", "Two.", "Three." }));
    }

    [Test]
    public void Clear_TakesOffTheAskItStandsFor_NotAnotherOfTheSameWords()
    {
        var owners = new WaitCriterion("0.1", "O", 2, "Three.", "approve") { Asked = "approve" };

        Assert.That(Criteria(WaitEdits.Clear(Text, [owners]))[2].Waiting!.Line, Is.EqualTo("Waiting: on legal: approve"));
    }

    [Test]
    public void Reassign_MovesTheAskToSomeoneByName_InTheOnForm()
    {
        var criteria = Criteria(WaitEdits.Reassign(Text, Both, "the gateway team"));

        Assert.That(criteria[0].Waiting!.Line, Is.EqualTo("Waiting: on the gateway team: name the agent; work: the audit store"));
        Assert.That(criteria[1].Waiting!.Line, Is.EqualTo("Waiting: on the gateway team: name the agent"));
    }

    [Test]
    public void Reassign_ToNoOne_GivesItBackToTheOwner()
    {
        var moved = WaitEdits.Reassign(Text, Both, "the gateway team");
        var back = new WaitCriterion("0.1", "O", 1, "Two.", "name the agent") { IsOwnerAsk = false, On = "the gateway team", Asked = "name the agent" };

        Assert.That(Criteria(WaitEdits.Reassign(moved, [back], null))[1].Waiting!.Line, Is.EqualTo("Waiting: owner: name the agent"));
    }

    [TestCase("Legal: Ana")]
    [TestCase("legal; and ops")]
    public void Reassign_ToAWhomTheFileCannotHold_IsRefused(string who)
    {
        Assert.Throws<ProjectEditRefusedException>(() => WaitEdits.Reassign(Text, Both, who));
    }

    [Test]
    public void Reassign_ToAWhomTooLong_IsRefused()
    {
        Assert.Throws<ProjectEditRefusedException>(() => WaitEdits.Reassign(Text, Both, new string('a', WaitingAsk.LongestWho + 1)));
    }

    [Test]
    public void AnAskNoLongerInTheFile_IsRefused_SoNothingIsWrittenOnAStaleView()
    {
        var stale = new WaitCriterion("0.1", "O", 1, "Two.", "an ask already answered");

        Assert.Throws<ProjectEditRefusedException>(() => WaitEdits.Clear(Text, [stale]));
    }
}
