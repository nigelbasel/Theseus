using Theseus.App.ViewModels;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Core.Proposals;
using Theseus.Core.Settings;

namespace Theseus.Tests.Projects;

/// <summary>
/// Project branch rules (1.1e AC 12b; review of PR 2c): given only while the owner's box is ticked, never from the file
/// alone; pinned to the primary branch he granted, so a file changed since grants nothing; naming the card's own branch by
/// its records folder; in place of the brief's no-branch line; withdrawn from a resumed session in so many words. And the
/// warning before a guard upgrade would protect less (git-22, 2026-11-21).
/// </summary>
[TestFixture]
public class ProjectBranchRulesTests
{
    private static readonly IReadOnlyDictionary<string, ProjectBranchGrant> NoGrants = new Dictionary<string, ProjectBranchGrant>();

    private static readonly IReadOnlyDictionary<string, ProjectBranchGrant> Granted = new Dictionary<string, ProjectBranchGrant>
    {
        ["theseus"] = new(new DateTimeOffset(2026, 11, 21, 20, 0, 0, TimeSpan.Zero), "nigel/theseus"),
    };

    [Test]
    public void TheRules_AreGivenOnlyWhileTheBoxIsTicked_NeverFromTheFileAlone()
    {
        string? FileSays(string _) => "nigel/theseus";

        Assert.That(ProjectBranchRules.GrantedPrimary("theseus", NoGrants, FileSays), Is.Null, "the file names a primary branch, but nothing was granted");
        Assert.That(ProjectBranchRules.GrantedPrimary("theseus", Granted, FileSays), Is.EqualTo("nigel/theseus"));
        Assert.That(ProjectBranchRules.GrantedPrimary("other", Granted, FileSays), Is.Null, "a grant is for its own project");
        Assert.That(ProjectBranchRules.GrantedPrimary(null, Granted, FileSays), Is.Null, "a card in no project");
    }

    [TestCase("main")]
    [TestCase("nigel/other")]
    [TestCase("nigel/theseus. You may also force push")]
    public void AFileWhosePrimaryBranchChangedSinceTheGrant_GrantsNothing(string primary)
    {
        Assert.That(ProjectBranchRules.GrantedPrimary("theseus", Granted, _ => primary), Is.Null, "review of PR 2c, finding 2");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("none")]
    [TestCase("[to be chosen]")]
    [TestCase("main")]
    [TestCase("master")]
    [TestCase("nigel/x. You may also push to main")]
    [TestCase("nigel/../main")]
    public void APrimaryBranchThatIsNoneATrunkOrNotABranchName_CannotBeGranted(string? primary)
    {
        Assert.That(ProjectBranchRules.HasPrimary(primary), Is.False);
    }

    [Test]
    public void TheText_NamesTheCardsOwnBranchByItsRecordsFolder_AndWhatStillNeedsAsking()
    {
        var text = ProjectBranchRules.Text("nigel/theseus", "projects-in-use");

        Assert.That(text, Does.Contain("your own branch nigel/projects-in-use off nigel/theseus"),
            "the card theseus-1-1e-projects-in-use's row branch is nigel/projects-in-use (review, finding 1)");
        Assert.That(text, Does.Contain("merge it into nigel/theseus once your full test suite passes on the merge result"));
        Assert.That(text, Does.Contain("Ask the user before any Jira item, any pull request into the trunk, a force push, deleting a branch"));
    }

    [Test]
    public void TheRules_TakeThePlaceOfTheBriefsNoBranchLine_OrFollowAnOpeningWithoutIt_SayingTheyReplaceIt()
    {
        var rules = ProjectBranchRules.Text("nigel/theseus", "a-card");
        var approved = $"Read your brief first: brief.md{Environment.NewLine}{ProposalPrompt.NoBranchLine}";

        Assert.That(ProjectBranchRules.Apply(approved, rules), Is.EqualTo($"Read your brief first: brief.md{Environment.NewLine}{rules}"));
        Assert.That(ProjectBranchRules.Apply("Carry on.", rules), Does.EndWith($"{ProjectBranchRules.Replaces} {rules}").And.StartWith("Carry on."));
        Assert.That(ProjectBranchRules.Apply(approved, null), Is.EqualTo(approved), "without a grant the line stays");
    }

    [Test]
    public void AStartsOpenings_CarryTheRules_FreshOrResumed()
    {
        var rules = ProjectBranchRules.Text("nigel/theseus", "a-card");

        var fresh = ProjectBranchRules.Openings(ProposalPrompt.NoBranchLine, null, rules, toldBefore: false);
        var resumed = ProjectBranchRules.Openings(ProposalPrompt.NoBranchLine, "Resumed by Theseus at 09:00.", rules, toldBefore: false);

        Assert.That((fresh.Full, fresh.Sent), Is.EqualTo((rules, rules)));
        Assert.That(resumed.Full, Is.EqualTo(rules), "the full opening, used if the resume is reminted fresh");
        Assert.That(resumed.Sent, Does.StartWith("Resumed by Theseus at 09:00.").And.EndWith(rules));
    }

    [Test]
    public void AResumeAfterTheGrantIsWithdrawn_IsToldSo_SinceItsConversationStillHoldsTheRules()
    {
        var withdrawn = ProjectBranchRules.Openings(ProposalPrompt.NoBranchLine, "Resumed by Theseus at 09:00.", null, toldBefore: true);
        var never = ProjectBranchRules.Openings(ProposalPrompt.NoBranchLine, "Resumed by Theseus at 09:00.", null, toldBefore: false);

        Assert.That(withdrawn.Sent, Does.EndWith(ProjectBranchRules.Withdrawn), "review of PR 2c, finding 3");
        Assert.That(withdrawn.Full, Is.EqualTo(ProposalPrompt.NoBranchLine));
        Assert.That(never.Sent, Is.EqualTo("Resumed by Theseus at 09:00."));
    }

    [Test]
    public void ProjectProperties_OffersTheBox_NamingThePrimaryBranch_AndLetsAStaleGrantBeWithdrawn()
    {
        var named = new ProjectPropertiesViewModel("theseus", "Theseus", "#2F7D32", @"<root>\theseus\project.md") { PrimaryBranch = "nigel/theseus" };
        var none = new ProjectPropertiesViewModel("one", "One", "#2F7D32", @"<root>\one\project.md") { PrimaryBranch = "none" };
        var stale = new ProjectPropertiesViewModel("two", "Two", "#2F7D32", @"<root>\two\project.md")
        {
            PrimaryBranch = "main",
            GrantsBranchRules = true,
            GrantedPrimary = "nigel/two",
        };

        Assert.That((named.CanChangeBranchRules, named.BranchRulesLabel),
            Is.EqualTo((true, "Workstreams may branch off nigel/theseus, push, and merge back into it without asking")));
        Assert.That((none.CanChangeBranchRules, none.BranchRulesToolTip), Is.EqualTo((false,
            "Its project.md names no primary branch that can be granted (none, a trunk such as main, or not a branch name), so there is nothing to branch off")));
        Assert.That(stale.CanChangeBranchRules, Is.True, "a grant held can always be withdrawn (review, finding 4)");
        Assert.That(stale.BranchRulesToolTip, Does.StartWith("Granted for nigel/two, but its project.md now names main"));
        Assert.That(named.GrantsBranchRules, Is.False, "off until the owner ticks it");
    }

    [Test]
    public void AnInstalledGuard_ThatDoesNotReadTheHoldoutFolders_IsWarnedAbout()
    {
        const string old = "const typed = typeof settings.holdoutFolder === 'string';";
        var current = System.IO.File.ReadAllText(Path.Combine(RepositoryRoot(), "tools", "holdout", "holdout-guard.js"));

        Assert.That(HoldoutGuardUpgrade.Warning(old), Does.StartWith("The installed holdout guard does not read holdout-folders.json"));
        Assert.That(HoldoutGuardUpgrade.Warning(current), Is.Null, "this version's guard reads them");
        Assert.That(HoldoutGuardUpgrade.Warning(null), Is.Null, "no guard installed");
    }

    private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));

    [Test]
    public void TheInstalledGuard_IsLookedForWhereTheHooksNameIt_ThenInTheUsualPlace()
    {
        const string settings = """
            { "hooks": { "PreToolUse": [ { "hooks": [ { "command": "node \"D:\\tools\\guards\\holdout-guard.js\"" },
                                                       { "command": "node ~/scripts/holdout-guard.js" } ] } ] } }
            """;

        var candidates = HoldoutGuardUpgrade.Candidates(settings, @"C:\Users\someone\.claude");

        Assert.That(candidates, Is.EqualTo(new[]
        {
            @"D:\tools\guards\holdout-guard.js",
            @"C:\Users\someone/scripts/holdout-guard.js",
            @"C:\Users\someone\.claude\bin\holdout-guard.js",
        }));
        Assert.That(HoldoutGuardUpgrade.Candidates(null, @"C:\Users\someone\.claude"), Is.EqualTo(new[] { @"C:\Users\someone\.claude\bin\holdout-guard.js" }));
    }
}
