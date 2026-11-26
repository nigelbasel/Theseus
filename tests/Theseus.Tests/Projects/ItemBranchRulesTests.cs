using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Core.Proposals;
using Theseus.Core.Settings;

namespace Theseus.Tests.Projects;

/// <summary>
/// The second kind of branch rules (2.4 W2, project-aware grants): a branch per item off its repository's trunk, pushed,
/// with a pull request into the trunk that the owner completes. Granted by the owner and pinned to the primary branch as
/// the first kind is; the trunk rules stay whatever the project says; tracker states are read from the tracker.
/// </summary>
[TestFixture]
public class ItemBranchRulesTests
{
    private static readonly IReadOnlyDictionary<string, ProjectBranchGrant> ItemGrant = new Dictionary<string, ProjectBranchGrant>
    {
        ["bug-fixing"] = new(new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.Zero), "nigel/bug-fixing") { Kind = BranchGrantKind.ItemBranches },
    };

    [Test]
    public void AGrant_IsOfTheFirstKind_UnlessTheOwnerChoseOtherwise()
    {
        Assert.That(new ProjectBranchGrant(DateTimeOffset.UnixEpoch, "nigel/x").Kind, Is.EqualTo(BranchGrantKind.MergeIntoPrimary));
    }

    [Test]
    public void TheText_GivesABranchOffTheTrunk_PushAndAPullRequest_NeverCompletingOne()
    {
        var text = ProjectBranchRules.Text("nigel/bug-fixing", "fix-nuf-1", BranchGrantKind.ItemBranches);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("your own branch nigel/fix-nuf-1 off its repository's trunk"));
            Assert.That(text, Does.Contain("raise a pull request from it into the trunk"));
            Assert.That(text, Does.Contain("Never complete, merge, approve, auto-complete or abandon a pull request into the trunk"));
            Assert.That(text, Does.Contain("never push to the trunk"));
            Assert.That(text, Does.Not.Contain("merge it into nigel/bug-fixing"), "the primary carries no code");
        });
    }

    [Test]
    public void TheText_SendsTheCardToTheTracker_ForTheStatesItMayMoveAnItemTo()
    {
        var text = ProjectBranchRules.Text("nigel/bug-fixing", "fix-nuf-1", BranchGrantKind.ItemBranches);

        Assert.That(text, Does.Contain("read them from the tracker").And.Contain("never from memory"));
        Assert.That(text, Does.Contain("Decisions table"));
    }

    [Test]
    public void TheFirstKind_IsUnchanged()
    {
        Assert.That(ProjectBranchRules.Text("nigel/theseus", "a-card", BranchGrantKind.MergeIntoPrimary),
            Is.EqualTo(ProjectBranchRules.Text("nigel/theseus", "a-card")));
    }

    [Test]
    public void TheItemGrant_IsPinnedToItsPrimaryBranch_AndReplacesTheBriefsNoBranchLine()
    {
        Assert.That(ProjectBranchRules.GrantedPrimary("bug-fixing", ItemGrant, _ => "nigel/other"), Is.Null);
        Assert.That(ProjectBranchRules.GrantedPrimary("bug-fixing", ItemGrant, _ => "nigel/bug-fixing"), Is.EqualTo("nigel/bug-fixing"));

        var rules = ProjectBranchRules.Text("nigel/bug-fixing", "fix-nuf-1", BranchGrantKind.ItemBranches);

        Assert.That(ProjectBranchRules.Apply(ProposalPrompt.NoBranchLine, rules), Is.EqualTo(rules));
    }

    [Test]
    public void ProjectProperties_OffersTheItemKind_OnlyWithTheGrant_OffByDefault()
    {
        var properties = new ProjectPropertiesViewModel("bug-fixing", "Bug Fixing", "#2F7D32", @"<root>\bug-fixing\project.md")
        {
            PrimaryBranch = "nigel/bug-fixing",
        };

        Assert.That((properties.GrantsItemBranches, properties.CanChooseItemBranches), Is.EqualTo((false, false)));

        properties.GrantsBranchRules = true;

        Assert.That(properties.CanChooseItemBranches, Is.True);

        properties.GrantsItemBranches = true;
        Assert.That(properties.BranchRulesValue, Does.Contain("a branch per item off its repository's trunk"));

        properties.GrantsBranchRules = false;
        Assert.That(properties.GrantsItemBranches, Is.False, "no grant, no kind");
    }
}
