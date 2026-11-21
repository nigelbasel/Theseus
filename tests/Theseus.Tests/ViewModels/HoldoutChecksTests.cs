using Theseus.App.ViewModels;
using Theseus.Core.Domain;
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

    [Test]
    public void AProjectWithNoScenarios_SaysSo_AndOffersWriteScenarios()
    {
        var checks = new HoldoutChecks(_root);
        checks.Refresh(string.Empty, [Card()]);

        Assert.That(checks.WhyCannotVerify(Card(), "A card"), Is.EqualTo("No holdout scenarios for this project yet"));
        Assert.That(checks.WayOn(Card()), Is.EqualTo(HoldoutWayOn.WriteScenarios));
    }

    [Test]
    public void ScenariosWritten_AreSeenAtTheNextRefresh_NotOnEveryQuery()
    {
        var checks = new HoldoutChecks(_root);
        checks.Refresh(string.Empty, [Card()]);
        Directory.CreateDirectory(Path.GetDirectoryName(Scenario("p"))!);
        File.WriteAllText(Scenario("p"), "x");

        Assert.That(checks.WhyCannotVerify(Card(), "A card"), Is.Not.Null, "the folder is not read again on a query");

        checks.Refresh(string.Empty, [Card()]);

        Assert.That((checks.WhyCannotVerify(Card(), "A card"), checks.WayOn(Card())), Is.EqualTo(((string?)null, HoldoutWayOn.None)));
    }

    [Test]
    public void AChosenFolderThatIsMissing_SaysSo_AndOffersSetTheHoldoutFolder()
    {
        var missing = Path.Combine(_root, "gone");
        var checks = new HoldoutChecks(_root);
        checks.Refresh(missing, [Card()]);

        Assert.That(checks.WhyCannotVerify(Card(), "A card"), Is.EqualTo($"The holdout folder {missing} is missing"));
        Assert.That(checks.WayOn(Card()), Is.EqualTo(HoldoutWayOn.SetFolder));
    }

    [Test]
    public void SetTheHoldoutFolder_IsNeverOfferedForTheDefault()
    {
        var checks = new HoldoutChecks(_root);
        checks.Refresh(string.Empty, [Card()]);

        Assert.That(checks.WayOn(Card()), Is.Not.EqualTo(HoldoutWayOn.SetFolder));
    }

    [Test]
    public void AVerifierOrScenarioWriterOrArchivedCard_IsNotVerified_AndOffersNothing()
    {
        var checks = new HoldoutChecks(_root);
        checks.Refresh(string.Empty, [Card()]);

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

        checks.Refresh(string.Empty, [Card(), writer]);

        Assert.That(checks.WayOn(Card()), Is.EqualTo(HoldoutWayOn.None));
        Assert.That(checks.WhyCannotVerify(Card(), "A card"), Is.EqualTo("No holdout scenarios for this project yet"));
    }
}
