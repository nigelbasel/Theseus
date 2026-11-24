using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ScaleFixtures;

namespace Theseus.Tests.Projects;

/// <summary>
/// Waits for you on a project of 35 objectives and no workstreams (project-view-scale, 2026-11-24: "a very big banner of
/// objectives", "many duplicates as well"): one line per thing asked of the owner, the work asks left out, and the
/// objectives nobody works on said as the lead's.
/// </summary>
[TestFixture]
public class ProjectOverviewScaleTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private ProjectOverview Overview()
    {
        var reading = WriteLarge(_folder);
        Assert.That(reading.IsReadable, reading.Reason);

        return ProjectOverview.For(reading.File!, [], [], [], 0);
    }

    [Test]
    public void AnAskManyCriteriaShare_IsOneLine_NamingHowManyAndWhere()
    {
        var shared = Overview().Waits.Where(wait => wait.Text.Contains(SharedAsk, StringComparison.Ordinal)).ToList();

        Assert.That(shared, Has.Count.EqualTo(1));
        Assert.That(shared[0].Text, Is.EqualTo($"{SharedAsk}: 70 criteria, in objectives 0.1, 0.2, 0.3 and 32 more. This happens outside Theseus."));
        Assert.That(shared[0].Objectives, Has.Count.EqualTo(Objectives), "the line belongs under every objective it names");
    }

    [Test]
    public void EachDifferentAsk_KeepsItsOwnLine_SoNothingAskedIsLost()
    {
        var waits = Overview().Waits;

        Assert.That(waits, Has.Count.EqualTo(Objectives + 1));
        Assert.That(waits.Select(wait => wait.Text), Has.Exactly(1).EqualTo($"Objective 1.3, Objective 1.3: {OwnAsk(7)}. This happens outside Theseus."));
    }

    [Test]
    public void NoLineRepeats_WhatItAsks_OfWhom()
    {
        var waits = Overview().Waits;

        Assert.That(waits.Select(wait => (wait.Kind, wait.CardKey, wait.Text)), Is.Unique);
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
        Assert.That(overview.Waits, Has.Count.EqualTo(Objectives + 1), "the lead's line is not counted among the owner's");
    }

    [Test]
    public void TheSentence_CountsTheGroupedLines()
    {
        Assert.That(Overview().Sentence, Is.EqualTo($"Nothing is moving. {Objectives + 1} things wait for you."));
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
    public void AnObjectivesWaits_IncludeTheSharedLine()
    {
        var overview = Overview();

        Assert.That(ObjectiveWaits.For("3.2", [], overview.Waits).Select(wait => wait.Text),
            Is.EqualTo(new[] { overview.Waits[0].Text, $"Objective 3.2, Objective 3.2: {OwnAsk(16)}. This happens outside Theseus." }));
    }
}
