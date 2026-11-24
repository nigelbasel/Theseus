using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ScaleFixtures;

namespace Theseus.Tests.Projects;

/// <summary>
/// Waits for you on a project of 35 objectives and no workstreams (project-view-scale, 2026-11-24: "a very big banner of
/// objectives", "many duplicates", "no way for me to take any action on them"): one line per thing asked of one person,
/// only the owner's counted, the work asks left out, a later phase's held under it, and the objectives nobody works on
/// said as the lead's.
/// </summary>
[TestFixture]
public class ProjectOverviewScaleTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private ProjectOverview Overview(IReadOnlyCollection<string>? names = null)
    {
        var reading = WriteLarge(_folder);
        Assert.That(reading.IsReadable, reading.Reason);

        return ProjectOverview.For(reading.File!, [], [], [], 0, ownerNames: names ?? Names);
    }

    private static WaitItem Line(IEnumerable<WaitItem> waits, string containing) =>
        waits.Single(wait => wait.Text.Contains(containing, StringComparison.Ordinal));

    [Test]
    public void AnAskManyCriteriaShare_IsOneLine_NamingHowManyAndWhere()
    {
        var shared = Line(Overview().Waits, SharedAsk);

        Assert.That(shared.Text, Is.EqualTo($"{SharedAsk} (ten criteria, in objectives 0.1, 0.2, 0.3 and two more)."));
        Assert.That((shared.Kind, shared.Button), Is.EqualTo((WaitKind.Decide, "Respond…")), "every wait on the owner has an action");
        Assert.That(shared.Criteria.Select(criterion => (criterion.Objective, criterion.Index)), Has.Exactly(10).Items);
    }

    [Test]
    public void OnlyThePhaseUnderWay_IsInTheBand_AndLaterPhasesAreHeldUnderTheirOwn()
    {
        var overview = Overview();

        Assert.That(overview.Waits, Has.Count.EqualTo(Yours));
        Assert.That(overview.Waits.SelectMany(wait => wait.Objectives), Has.All.StartWith("0."));
        Assert.That(overview.Held.Keys, Is.EquivalentTo(new[] { 1, 2, 3, 4, 5, 6 }));
        Assert.That(overview.Held[3].Select(wait => wait.Text), Has.Exactly(1).EqualTo($"{SharedAsk} (ten criteria, in objectives 3.1, 3.2, 3.3 and two more)."));
    }

    [Test]
    public void NoLineSaysItHappensOutsideTheseus_WithNothingToPress()
    {
        Assert.That(Overview().Waits.Select(wait => (wait.Text, wait.Button)), Has.None.Matches<(string Text, string? Button)>(line =>
            line.Text.Contains("outside Theseus", StringComparison.Ordinal) || line.Button is null));
    }

    [Test]
    public void AnAskOnSomeoneByName_IsTheirs_NotCounted()
    {
        var overview = Overview();
        var team = Line(overview.Others, Team);

        Assert.That((team.Who, team.Text), Is.EqualTo((Team, $"Waits on {Team}: an answer (objective 0.1, Objective 0.1).")));
        Assert.That(overview.Waits.Select(wait => wait.Text), Has.None.Contains(Team));
    }

    [Test]
    public void AnOlderLineNamingAnotherTeam_IsTheirs()
    {
        var legacy = Line(Overview().Others, LegacyTeam);

        Assert.That((legacy.Who, legacy.Ask), Is.EqualTo((LegacyTeam, "the EC answers")));
    }

    [Test]
    public void AnOlderLineNamingTheOwnerWithOthers_IsHis()
    {
        Assert.That(Line(Overview().Waits, "latency budget").Kind, Is.EqualTo(WaitKind.Decide));
        Assert.That(Overview(names: []).Others.Select(wait => wait.Who), Has.Member("Product team with Owner"),
            "without the owner's names, the same line reads as the team's: the names are what make it his");
    }

    [Test]
    public void TheOwnersFullAndFirstNames_ComeFromTheAccount()
    {
        Assert.That(OwnerNames.From("ada", "Ada Lovelace"), Is.EqualTo(new[] { "ada", "Ada Lovelace" }),
            "first name and account differ only in case, so one stands for both");
        Assert.That(OwnerNames.From("jb", "Jo Bloggs"), Is.EqualTo(new[] { "jb", "Jo Bloggs", "Jo" }));
        Assert.That(OwnerNames.From("jb", null), Is.EqualTo(new[] { "jb" }));
    }

    [Test]
    public void NoLineRepeats_WhatItAsks_OfWhom()
    {
        var overview = Overview();

        Assert.That(overview.Waits.Concat(overview.Others).Select(wait => (wait.Kind, wait.Who, wait.Text)), Is.Unique);
    }

    [Test]
    public void AWorkAskOnAnOwnerLine_IsNotPutToTheOwner()
    {
        Assert.That(Overview().Waits.Select(wait => wait.Text), Has.None.Contains("work:").And.None.Contains("audit store"));
    }

    [Test]
    public void ObjectivesWithNoWorkstream_WaitOnTheLead_NotInWaitsForYou()
    {
        var overview = Overview();

        Assert.That(overview.ForTheLead, Is.EqualTo(new[] { "35 objectives have no workstream yet." }));
        Assert.That(overview.Waits, Has.Count.EqualTo(Yours), "the lead's line is not counted among the owner's");
    }

    [Test]
    public void TheSentence_CountsOnlyTheOwnersWaits()
    {
        Assert.That(Overview().Sentence, Is.EqualTo("Nothing is moving. Seven things wait for you."));
    }

    [Test]
    public void ACardWorkingTwoPieces_AsksItsQuestionOnce()
    {
        _folder.Write("two/project.md", TestProjectsFolder.ProjectText("Two", body: TestProjectsFolder.OneObjective("first-piece", "second-piece")));
        var reading = ProjectViewFixtures.Read(_folder, "two");
        var pieces = PiecesOfWork.Build(reading, _ => null, [ProjectViewFixtures.Card("A workstream", Theseus.Core.Domain.SessionLifecycle.NeedsAttention)], []);

        var answers = ProjectOverview.For(reading.File!, pieces, [], [], 0).Waits.Where(wait => wait.Kind == WaitKind.Answer).ToList();

        Assert.That(pieces, Has.Count.EqualTo(2));
        Assert.That(answers, Has.Count.EqualTo(1));
    }

    [Test]
    public void AnObjectivesWaits_IncludeTheSharedLine_AndItsOwn()
    {
        var overview = Overview();

        Assert.That(ObjectiveWaits.For("0.2", [], overview.Waits).Select(wait => wait.Text),
            Is.EqualTo(new[] { Line(overview.Waits, SharedAsk).Text, $"{OwnAsk(1)} (objective 0.2, Objective 0.2)." }));
    }
}
