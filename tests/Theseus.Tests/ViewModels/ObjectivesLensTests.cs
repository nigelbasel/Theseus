using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Core.Settings;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ClarityFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The objectives lens is the main view (Nigel, 2026-11-22): a project opens on it unless a choice is remembered, and
/// every phase and objective carries a rolled-up mark with who is working on it and the three answers in its hover.
/// </summary>
[TestFixture]
public class ObjectivesLensTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private ProjectViewModel Bym(MemorySettingsStore settings) =>
        ProjectViewModel.Build(WriteBym(_folder), BymPieces(_folder), ProjectViewFixtures.LinksOn(BymLinks(), BymBoard()), [], settings,
            new ProjectViews(_folder).Actions);

    [Test]
    public void AProject_OpensOnTheObjectivesLens_UnlessAChoiceIsRemembered()
    {
        Assert.That(Bym(new MemorySettingsStore()).IsGroupedByObjectives, Is.True);

        var remembered = new MemorySettingsStore();
        remembered.Write(remembered.Read() with
        {
            ProjectGrouping = new Dictionary<string, ProjectViewGrouping> { [ClarityFixtures.Bym] = ProjectViewGrouping.Flow },
        });

        Assert.That(Bym(remembered).IsGroupedByFlow, Is.True);
    }

    [Test]
    public void EachObjective_RollsUp_MetWaitingInProgressPartlyOrNotStarted()
    {
        var view = Bym(new MemorySettingsStore());
        RollupState StateOf(string number) => view.AllObjectives.Single(heading => heading.Objective?.Number == number).Rollup!.State;

        Assert.That(StateOf("0.1"), Is.EqualTo(RollupState.Met));
        Assert.That(StateOf("1.2"), Is.EqualTo(RollupState.WaitingForYou), "its builds wait for their independent check, which only he starts");
        Assert.That(StateOf("1.7"), Is.EqualTo(RollupState.WaitingForYou), "W6 builds ahead of its spec: waiting for you wins over in progress");
        Assert.That(StateOf("1.5"), Is.EqualTo(RollupState.NotStarted));
        Assert.That(StateOf("1.10"), Is.EqualTo(RollupState.PartlyMet));
    }

    [Test]
    public void AnObjectivesMark_NamesWhoIsWorkingOnIt_AndItsHoverGivesTheThreeAnswers()
    {
        var heading = Bym(new MemorySettingsStore()).AllObjectives.Single(each => each.Objective?.Number == "1.4");

        Assert.That(heading.RollupText, Does.Contain("BYM MCP W4 - MCP host and staff plugin"), "W4's session is busy");
        Assert.That(heading.Rollup!.ToolTip, Does.StartWith("Waiting for you:").And.Contain("You act"));
    }

    [Test]
    public void APhase_RollsUpItsObjectivesTheSameWay()
    {
        var phases = Bym(new MemorySettingsStore()).Phases;

        Assert.That(phases[0].Rollup.State, Is.EqualTo(RollupState.Met));
        Assert.That(phases[1].Rollup.State, Is.EqualTo(RollupState.WaitingForYou));
        Assert.That(Rollup.Of([]).State, Is.EqualTo(RollupState.NotStarted), "a phase with no objectives");
    }

    [Test]
    public void RecordedWork_WithNothingTicked_IsInProgress_NotNotStarted()
    {
        var file = ProjectFileReader.Read("p", TestProjectsFolder.ProjectText("P", body: "## Phase 0: S\n\n### Objective 0.1: O\n"
            + "- [ ] One.\n\n**Pipeline:** freeform\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n")).File!;
        var next = ProjectViewFixtures.OnePiece(_folder, "feature", ProjectViewFixtures.Files("spec.md"), accepted: true);

        Assert.That(next.Status.Text, Does.StartWith("Next:"));
        Assert.That(Rollup.Of(file.Objectives[0], [next], []).State, Is.EqualTo(RollupState.InProgress), "review of PR 5, finding 2");
    }

    [Test]
    public void TheWhoOfAMark_IsCappedAtTwoNames()
    {
        var rollup = new Rollup(RollupState.InProgress, "● In progress", ["A", "B", "C", "D"], "tip");

        Assert.That(rollup.Who, Is.EqualTo("A, B and 2 more"));
    }

    [Test]
    public void AnOwnersWaitingLine_MakesItsObjectiveWaitForYou()
    {
        var file = ProjectFileReader.Read("p", TestProjectsFolder.ProjectText("P", body: "## Phase 0: S\n\n### Objective 0.1: O\n"
            + "- [x] One.\n- [ ] Two.\n      Waiting: owner: install the guard\n\n**Pipeline:** freeform\n"
            + "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n")).File!;

        Assert.That(Rollup.Of(file.Objectives[0], [], []).State, Is.EqualTo(RollupState.WaitingForYou));
    }
}
