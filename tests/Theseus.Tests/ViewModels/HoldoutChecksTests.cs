using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Tests.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The shell's own rules for Verify with holdouts (1.1e AC 6a, 6c; review of PR 2b, findings 3, 5 and 8): the reason it
/// cannot run, and the way on, from the holdout folders as last refreshed, never read again on a command query.
/// </summary>
[TestFixture]
public class HoldoutChecksTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "theseus-holdout-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    private static Workstream Card(string key = "a-card", string? project = "p", WorkstreamRole role = WorkstreamRole.None,
        bool archived = false) => ProjectCards.Card(key, project, role: role, archived: archived);

    private string Scenario(string project) =>
        Path.Combine(_root, "Theseus", "holdouts", project, "scenarios", "stop.md");

    // The folder chosen for project "p", or none.
    private static Dictionary<string, string> Chosen(string? folder = null) =>
        folder is null ? [] : new Dictionary<string, string> { ["p"] = folder };

    [Test]
    public void ARecordedRepositoryWithNoClone_RefusesVerifyWithItsReason_AndOffersSetTheHoldoutFolder()
    {
        var home = Path.Combine(_root, "src", "Repo");
        Directory.CreateDirectory(home);
        var checks = new HoldoutChecks(_root);
        checks.Refresh(Chosen(), _ => new HoldoutConvention(home, new RecordedHoldout(null)), [Card()]);

        Assert.That(checks.WhyCannotVerify(Card(), "A card"), Is.EqualTo(
            $"project.md records a holdout repository, but no clone was found at {Path.Combine(_root, "src", "Repo-holdout")}: clone it there, or choose a folder in Project properties"));
        Assert.That(checks.WayOn(Card()), Is.EqualTo(HoldoutWayOn.SetFolder));
        Assert.That(checks.LocationOf(Card()).IsDefault, Is.False, "never Theseus's own, which would judge against an empty folder");
    }

    [Test]
    public void ARecordedRepositoryWithNoHomeClone_SaysNeitherIsFound()
    {
        var checks = new HoldoutChecks(_root);
        checks.Refresh(Chosen(), _ => new HoldoutConvention(null, new RecordedHoldout("Repo-holdout")), [Card()]);

        Assert.That(checks.WhyCannotVerify(Card(), "A card"),
            Is.EqualTo("project.md records a holdout repository, but the home repo's clone is not found, so neither is it"));
    }

    [Test]
    public void CardsInNoProject_AlwaysUseTheseussOwnAdHocFolder_WhateverIsChosen()
    {
        var checks = new HoldoutChecks(_root);
        checks.Refresh(new Dictionary<string, string> { [""] = Path.Combine(_root, "mine") },
            _ => new HoldoutConvention(_root, new RecordedHoldout(null)), [Card(project: null)]);

        Assert.That(checks.LocationOf(Card(project: null)).Folder, Is.EqualTo(Path.Combine(_root, "Theseus", "holdouts", "ad-hoc")));
    }

    [Test]
    public void AProjectWithNoScenarios_SaysSo_AndOffersWriteScenarios()
    {
        var checks = new HoldoutChecks(_root);
        checks.Refresh(Chosen(), _ => HoldoutConvention.None, [Card()]);

        Assert.That(checks.WhyCannotVerify(Card(), "A card"), Is.EqualTo("No holdout scenarios for this project yet"));
        Assert.That(checks.WayOn(Card()), Is.EqualTo(HoldoutWayOn.WriteScenarios));
    }

    [Test]
    public void ScenariosWritten_AreSeenAtTheNextRefresh_NotOnEveryQuery()
    {
        var checks = new HoldoutChecks(_root);
        checks.Refresh(Chosen(), _ => HoldoutConvention.None, [Card()]);
        Directory.CreateDirectory(Path.GetDirectoryName(Scenario("p"))!);
        File.WriteAllText(Scenario("p"), "Checks: a-card");

        Assert.That(checks.WhyCannotVerify(Card(), "A card"), Is.Not.Null, "the folder is not read again on a query");

        checks.Refresh(Chosen(), _ => HoldoutConvention.None, [Card()]);

        Assert.That((checks.WhyCannotVerify(Card(), "A card"), checks.WayOn(Card())), Is.EqualTo(((string?)null, HoldoutWayOn.None)));
    }

    [Test]
    public void ScenariosThatNameOtherWork_MakeNoVerifierForThisBuild_AndSayWhy()
    {
        var checks = new HoldoutChecks(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(Scenario("p"))!);
        File.WriteAllText(Scenario("p"), "# Stop with handoff\nChecks: another-card\n");
        checks.Refresh(Chosen(), _ => HoldoutConvention.None, [Card()]);

        Assert.That(checks.WhyCannotVerify(Card(), "A card"), Is.EqualTo(
            "No holdout scenario names a-card: a scenario says which work it checks in a line such as \"Checks: a-card\""),
            "git-22, 2026-11-23: BYM's verifiers ran Theseus's scenarios because one folder judged every build");
        Assert.That(checks.WayOn(Card()), Is.EqualTo(HoldoutWayOn.WriteScenarios));
    }

    [Test]
    public void AScenarioNamingTheBuildsSlug_LetsItBeVerified()
    {
        var card = ProjectCards.Card("a-card", "p", slug: "verifier-runs");
        var checks = new HoldoutChecks(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(Scenario("p"))!);
        File.WriteAllText(Scenario("p"), "Checks: close-step, Verifier-Runs");
        checks.Refresh(Chosen(), _ => HoldoutConvention.None, [card]);

        Assert.That(checks.WhyCannotVerify(card, "A card"), Is.Null);
    }

    [Test]
    public void AChosenFolderThatIsMissing_SaysSo_AndOffersSetTheHoldoutFolder()
    {
        var missing = Path.Combine(_root, "gone");
        var checks = new HoldoutChecks(_root);
        checks.Refresh(Chosen(missing), _ => HoldoutConvention.None, [Card()]);

        Assert.That(checks.WhyCannotVerify(Card(), "A card"), Is.EqualTo($"The holdout folder {missing} is missing"));
        Assert.That(checks.WayOn(Card()), Is.EqualTo(HoldoutWayOn.SetFolder));
    }

    [Test]
    public void SetTheHoldoutFolder_IsNeverOfferedForTheDefault()
    {
        var checks = new HoldoutChecks(_root);
        checks.Refresh(Chosen(), _ => HoldoutConvention.None, [Card()]);

        Assert.That(checks.WayOn(Card()), Is.Not.EqualTo(HoldoutWayOn.SetFolder));
    }

    [Test]
    public void AVerifierOrScenarioWriterOrArchivedCard_IsNotVerified_AndOffersNothing()
    {
        var checks = new HoldoutChecks(_root);
        checks.Refresh(Chosen(), _ => HoldoutConvention.None, [Card()]);

        Assert.That(checks.WhyCannotVerify(Card(role: WorkstreamRole.Verifier), "V"), Is.EqualTo("V checks or writes holdout scenarios itself"));
        Assert.That(checks.WhyCannotVerify(Card(role: WorkstreamRole.ScenarioWriter), "W"), Is.EqualTo("W checks or writes holdout scenarios itself"));
        Assert.That(checks.WhyCannotVerify(Card(archived: true), "A"), Is.EqualTo("A is archived"));
        Assert.That(checks.WayOn(Card(archived: true)), Is.EqualTo(HoldoutWayOn.None));
    }

    [Test]
    public void WhileACardIsWritingTheScenarios_WriteScenariosIsNotOfferedAgain()
    {
        var checks = new HoldoutChecks(_root);
        var writer = Card("writer", null, WorkstreamRole.ScenarioWriter) with
        {
            WorkingDirectory = Path.Combine(_root, "Theseus", "holdouts", "p"),
        };

        checks.Refresh(Chosen(), _ => HoldoutConvention.None, [Card(), writer]);

        Assert.That(checks.WayOn(Card()), Is.EqualTo(HoldoutWayOn.None));
        Assert.That(checks.WhyCannotVerify(Card(), "A card"), Is.EqualTo("No holdout scenarios for this project yet"));
    }

    [Test]
    public void AFolderThatCannotBeRead_LeavesWhetherABuildIsNamedUnknown_NotUnnamed()
    {
        var checks = new HoldoutChecks(_root);
        checks.Refresh(Chosen(Path.Combine(_root, "gone")), _ => HoldoutConvention.None, [Card()]);

        Assert.That(checks.Names(Card()), Is.Null,
            "review of the follow-ups, M2: a missing folder made every verifier of the project look stranded");
    }

    [Test]
    public void AFolderWithScenariosNamingOtherWork_SaysTheBuildIsNotNamed()
    {
        var scenario = Scenario("p");
        Directory.CreateDirectory(Path.GetDirectoryName(scenario)!);
        File.WriteAllText(scenario, "# Stop\n\nChecks: other-work\n");
        var checks = new HoldoutChecks(_root);
        checks.Refresh(Chosen(), _ => HoldoutConvention.None, [Card()]);

        Assert.That(checks.Names(Card()), Is.False);
        Assert.That(checks.Names(Card("other-work")), Is.True);
    }
}
