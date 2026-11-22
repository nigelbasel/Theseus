using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Core.Settings;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Group by: Flow or Objectives (1.1d AC 45 to 51; Nigel's decision 5, 2026-11-21): the choice kept per project, phases
/// and objectives as headings with their counts and criteria, the Flow's own boxes under them with marks for what the
/// Flow draws as lines, the toolbar's fold buttons acting on headings too, the headings' menus and Properties, and their
/// folds remembered.
/// </summary>
[TestFixture]
public class ProjectViewGroupingTests
{
    private const string Grouped = "grouped";

    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
        Redraws = 0;
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private IReadOnlyList<string> Stored(string project) =>
        _views.Settings.Settings.ViewFolds.GetValueOrDefault(PieceFolds.KeyFor(project)) ?? [];

    private static ObjectiveHeadingViewModel Objective(ProjectViewModel view, string number) =>
        view.AllObjectives.Single(objective => objective.Number == number);

    private static PhaseHeadingViewModel Phase(ProjectViewModel view, int number) => view.Phases.Single(phase => phase.Number == number);

    private ProjectViewModel Objectives(ProjectViewModel view)
    {
        view.IsGroupedByObjectives = true;

        return view;
    }

    // A done phase with an exit gate, then a phase whose one objective nothing works on yet.
    private ProjectViewModel GroupedView()
    {
        const string table = "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n";

        _folder.Write($"{Grouped}/project.md", TestProjectsFolder.ProjectText("Grouped", body:
            "## Phase 0: Started\n\n**Exit gate:** The first objective is met.\n\n"
            + "### Objective 0.1: Finished\n- [x] Ticked.\n\n**Pipeline:** feature\n" + table
            + $"| {ACard} | {APiece} | owner/{APiece} | a-repo |\n\n"
            + "## Phase 1: Next\n\n### Objective 1.1: Not begun\n- [ ] One.\n- [x] Two.\n\n**Pipeline:** [to be chosen]\n" + table));

        return _views.Of(Read(_folder, Grouped), [Card(ACard)]);
    }

    [Test]
    public void GroupBy_StartsAsTheFlow_AndObjectivesIsKeptForThatProjectAcrossARestart()
    {
        var view = _views.Theseus();

        Assert.That((view.IsGroupedByFlow, view.IsGroupedByObjectives), Is.EqualTo((true, false)));

        view.IsGroupedByObjectives = true;

        Assert.That((view.IsGroupedByFlow, view.Grouping), Is.EqualTo((false, ProjectViewGrouping.Objectives)));
        Assert.That(_views.Settings.Settings.ProjectGrouping[TheseusProject], Is.EqualTo(ProjectViewGrouping.Objectives));
        Assert.That(_views.Theseus().IsGroupedByObjectives, Is.True, "a restart keeps it");
        Assert.That(_views.BuildYourMarket().IsGroupedByFlow, Is.True, "another project keeps its own");
    }

    [Test]
    public void UntickingTheChosenGrouping_KeepsIt_AndTellsTheViewAgain()
    {
        var view = _views.Theseus();
        var raised = new List<string?>();
        view.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        var writes = _views.Settings.Writes;

        view.IsGroupedByFlow = false;

        Assert.That(view.IsGroupedByFlow, Is.True);
        Assert.That(raised, Does.Contain(nameof(ProjectViewModel.IsGroupedByFlow)), "a toggle unticked by a click is ticked again");
        Assert.That(_views.Settings.Writes, Is.EqualTo(writes), "nothing is written");
    }

    [Test]
    public void GoingBack_ToAFlowPlace_RestoresTheFlowLens()
    {
        var view = _views.Theseus();
        view.IsGroupedByObjectives = true;

        view.GoTo(nameof(ProjectViewGrouping.Flow), null);

        Assert.That(view.IsGroupedByFlow, Is.True, "review of PR 5, finding 6");
    }

    [Test]
    public void TheGrouping_ReachesTheSignature_SoARefreshAfterSwitchingDoesNotRebuildAgain()
    {
        var view = _views.Theseus();
        var before = view.Signature;

        view.IsGroupedByObjectives = true;

        Assert.That(view.Signature, Is.Not.EqualTo(before));
        Assert.That(_views.Theseus().Signature, Is.EqualTo(view.Signature));
    }

    [Test]
    public void OnlyTheChosenGrouping_IsLaidOut()
    {
        var view = _views.BuildYourMarket();

        Assert.That((view.FlowRoots, view.ShownPhases, view.ShownUnlisted), Is.EqualTo((view.Roots, Array.Empty<PhaseHeadingViewModel>(),
            Array.Empty<ObjectiveHeadingViewModel>())));

        view.IsGroupedByObjectives = true;

        Assert.That(view.FlowRoots, Is.Empty);
        Assert.That(view.ShownPhases, Is.EqualTo(view.Phases));
        Assert.That(view.ShownUnlisted.Single().Title, Is.EqualTo("Not in project.md"));
    }

    [Test]
    public void OnTheseus_EveryPhaseAndObjective_IsAHeading_WithItsCountsAndCriteria()
    {
        var view = Objectives(_views.Theseus());

        Assert.That(view.Phases.Select(phase => (phase.Title, phase.DoneText)), Is.EqualTo(new[]
        {
            ("Phase 0: Seed the method", "0 of 3 objectives done"), ("Phase 1: Evidence", "0 of 2 objectives done"),
        }));
        Assert.That(view.AllObjectives.Select(objective => objective.Title), Is.EqualTo(new[]
        {
            "0.1 Method kit v0", "0.2 Holdout scenarios", "0.3 Five changes through the method by hand", "1.0 A verifier card",
            "1.1 Checks shown on cards",
        }));
        Assert.That(view.AllObjectives.Select(objective => objective.CriteriaText), Has.All.EqualTo("0/1"));
        Assert.That(Objective(view, "0.1").Counts.Select(chip => chip.Text), Is.EqualTo(new[] { "✓ 1 done" }));
        Assert.That(Objective(view, "1.1").Counts.Select(chip => chip.Text), Is.EqualTo(new[] { "◆ 3 waiting on you" }), "zeros left out");
        Assert.That(view.Unlisted, Is.Empty);
    }

    [Test]
    public void OnBuildYourMarket_EveryObjectiveIsAHeading_AnEmptyPhaseSaysSo_AndTheUnlistedPiecesHaveTheirOwnHeading()
    {
        var view = Objectives(_views.BuildYourMarket());

        Assert.That(view.Phases.Select(phase => (phase.Title, phase.DoneText, phase.IsOpen)), Is.EqualTo(new[]
        {
            ("Phase 0: Specify", "1 of 1 objectives done", false), ("Phase 1: Build read-only, internal", "0 of 6 objectives done", true),
            ("Phase 2: Customer release", "no objectives yet", true),
        }));
        Assert.That(Phase(view, 0).ExitGate, Is.EqualTo("Exit gate: Nigel approves the spec."));
        Assert.That(Objective(view, "0.1").CriteriaText, Is.EqualTo("1/1"));
        Assert.That(Objective(view, "1.2").Counts.Select(chip => chip.Text), Is.EqualTo(new[] { "◐ 2 holdouts not run" }));
        Assert.That(new[] { "1.5", "1.6" }.Select(number => Objective(view, number).IsEmpty), Has.All.True, "they say nothing yet");

        var unlisted = view.Unlisted.Single();

        Assert.That((unlisted.Title, unlisted.CriteriaText), Is.EqualTo(("Not in project.md", string.Empty)));
        Assert.That(unlisted.Pieces.Select(piece => piece.Name), Is.EqualTo(new[] { RedirectAnchoring, ClientFixes }), "in link order, after the listed pieces");
        Assert.That(unlisted.FoldName, Is.EqualTo("unlisted:"));
        Assert.That(unlisted.Counts.Select(chip => chip.Text), Is.EqualTo(new[] { "+ 2 not in project.md" }));
    }

    [Test]
    public void AnObjectiveWithNoPieces_SaysNothingYet_AndADonePhaseStartsFolded()
    {
        var view = Objectives(GroupedView());
        var empty = Objective(view, "1.1");

        Assert.That((empty.IsEmpty, empty.CriteriaText, empty.ShowsPieces), Is.EqualTo((true, "1/2", false)));
        Assert.That(Phase(view, 0).ExitGate, Is.EqualTo("Exit gate: The first objective is met."));
        Assert.That((Phase(view, 0).IsDone, Phase(view, 0).IsOpen), Is.EqualTo((true, false)), "a done phase starts folded");
        Assert.That(Phase(view, 1).IsOpen, Is.True);
        Assert.That(view.AllObjectives.Select(objective => objective.IsOpen), Has.All.True, "objectives start open");
        Assert.That(Phase(view, 1).HasExitGate, Is.False);
    }

    [Test]
    public void UnderEachObjective_AreTheFlowsOwnBoxes_EachOnce()
    {
        var view = Objectives(_views.BuildYourMarket());
        var boxes = view.AllObjectives.SelectMany(objective => objective.Pieces).ToList();

        Assert.That(boxes, Is.EquivalentTo(view.AllNodes), "the same boxes, with their actions, folds and Properties");
        Assert.That(boxes, Is.Unique);
    }

    [Test]
    public void TheBoxes_CarryTheMarksOfHandOversAcrossObjectives()
    {
        var view = _views.BuildYourMarket();
        string[] Marks(string piece) => [.. view.AllNodes.Single(node => node.Name == piece).CrossMarks];

        Assert.That(Marks("bym-mcp-read-api"), Is.EqualTo(new[] { $"← handed on from {ServerSpec}" }));
        Assert.That(Marks(ServerSpec).Single(), Does.StartWith("→ handed on to bym-mcp-preconditions, bym-mcp-identity"));
        Assert.That(Marks(RedirectAnchoring), Is.EqualTo(new[] { "← handed on from bym-mcp-identity" }));
        Assert.That(view.AllNodes.Single(node => node.Name == "bym-mcp-read-api").ReportBackText,
            Is.EqualTo($"↩ reported back to {ServerSpec}"), "report-back marks are as in the Flow");
    }

    [Test]
    public void CollapseAllAndExpandAll_ActOnTheHeadingsAndTheBoxesTogether()
    {
        var view = Objectives(_views.Theseus());

        view.CollapseAllCommand.Execute(null);

        Assert.That(view.Phases.Select(phase => phase.IsOpen).Concat(view.AllObjectives.Select(objective => objective.IsOpen))
            .Concat(view.AllNodes.Select(node => node.IsOpen)), Has.All.False);
        Assert.That(Stored(TheseusProject), Is.EqualTo(new[] { "*=folded" }));

        view.ExpandAllCommand.Execute(null);

        Assert.That(view.Phases.Select(phase => phase.IsOpen).Concat(view.AllObjectives.Select(objective => objective.IsOpen))
            .Concat(view.AllNodes.Select(node => node.IsOpen)), Has.All.True);
        Assert.That(Stored(TheseusProject), Is.EqualTo(new[] { "*=open" }));
    }

    [Test]
    public void OpenWhatNeedsAttention_OpensTheHeadingsOnTheWayToIt_AndFoldsTheRest()
    {
        var view = Objectives(_views.Theseus());
        view.ExpandAllCommand.Execute(null);

        view.OpenWhatNeedsAttentionCommand.Execute(null);

        Assert.That(view.AllNodes.Where(node => node.IsOpen).Select(node => node.Name),
            Is.EqualTo(new[] { "project-view", "project-home-folders", "projects-and-method-ui" }));
        Assert.That(view.AllObjectives.Where(objective => objective.IsOpen).Select(objective => objective.Number), Is.EqualTo(new[] { "1.1" }));
        Assert.That(view.Phases.Select(phase => phase.IsOpen), Is.EqualTo(new[] { false, true }));
        Assert.That(Stored(TheseusProject), Does.Contain("phase:Seed the method=folded").And.Contain("objective:Checks shown on cards=open")
            .And.Contain("project-view=open"));
        Assert.That(Stored(TheseusProject), Has.Count.EqualTo(12 + 5 + 2 + 1 + 2),
            "every box and every heading, the one round, and its two runs of finished pieces (1.1f C13, D22)");
    }

    [Test]
    public void OpenWhatNeedsAttention_InTheFlow_KeepsTheHeadingsAsTheyWereLeft()
    {
        var view = Objectives(_views.Theseus());
        Phase(view, 0).ToggleCommand.Execute(null);
        view.IsGroupedByFlow = true;

        view.OpenWhatNeedsAttentionCommand.Execute(null);

        Assert.That(Stored(TheseusProject), Does.Contain("phase:Seed the method=folded"));
        Assert.That(Stored(TheseusProject), Has.Count.EqualTo(13));
        Assert.That(Phase(view, 0).IsOpen, Is.False);
    }

    [Test]
    public void AHeadingsFold_IsRememberedWithTheBoxes_AcrossARestart()
    {
        var view = Objectives(_views.Theseus());

        Phase(view, 1).ToggleCommand.Execute(null);
        Objective(view, "0.3").ToggleCommand.Execute(null);

        Assert.That(Stored(TheseusProject), Is.EqualTo(new[] { "phase:Evidence=folded", "objective:Five changes through the method by hand=folded" }),
            "kept by name, so a phase added before them moves no fold");

        var restarted = _views.Theseus();

        Assert.That((Phase(restarted, 1).IsOpen, Objective(restarted, "0.3").IsOpen, Objective(restarted, "0.1").IsOpen),
            Is.EqualTo((false, false, true)));
        Assert.That((Phase(restarted, 1).Chevron, Phase(restarted, 1).ChevronToolTip), Is.EqualTo(("▸", "Show this phase's objectives")));
    }

    [Test]
    public void AnObjectiveHeading_FillsPropertiesWithItsObjective_ItsCriteriaToTick_AndEditObjective()
    {
        var view = Objectives(_views.Theseus());
        var heading = Objective(view, "1.1");

        heading.SelectCommand.Execute(null);
        var pane = view.Properties;

        Assert.That((view.Selection, heading.IsSelected), Is.EqualTo((heading, true)));
        Assert.That((pane.Heading, pane.StatusText), Is.EqualTo(("Objective 1.1: Checks shown on cards", "0 of 1 criteria ticked")));
        Assert.That(pane.Rows.Select(row => (row.Label, row.Value)), Is.EqualTo(new[]
        {
            ("Phase", "Phase 1: Evidence"), ("Pieces", "project-view, project-home-folders, projects-and-method-ui"),
            ("Where", "◆ 3 waiting on you"),
        }));
        Assert.That(pane.Criteria.Select(criterion => (criterion.TickGlyph, criterion.Text)), Is.EqualTo(new[] { ("☐", "A criterion.") }));

        pane.Criteria.Single().ToggleCommand!.Execute(null);
        pane.EditObjectiveCommand!.Execute(null);

        var line = heading.Objective!.Criteria.Single().Line;
        Assert.That(_views.Edited, Is.EqualTo(new[] { $"Tick:{line}:True", "Edit:1.1" }));
    }

    [Test]
    public void AnObjectiveHeadingsMenu_HoldsPropertiesThenEditObjective()
    {
        var view = Objectives(_views.Theseus());
        var heading = Objective(view, "0.3");

        Assert.That(heading.MenuItems.Select(item => (item.Label, item.IsDefault)), Is.EqualTo(new[]
        {
            ("Properties", true), ("Edit objective…", false),
        }));

        heading.MenuItems[0].Command.Execute(null);
        Assert.That(view.Selection, Is.SameAs(heading));

        heading.MenuItems[1].Command.Execute(null);
        Assert.That(_views.Edited, Is.EqualTo(new[] { "Edit:0.3" }));
    }

    [Test]
    public void APhaseHeadingsMenu_HasAddObjective_ForThatPhase_AndSoDoesItsProperties()
    {
        var view = Objectives(_views.Theseus());
        var phase = Phase(view, 1);

        Assert.That(phase.MenuItems.Select(item => item.Label), Is.EqualTo(new[] { "Properties", "Add objective…" }));

        phase.MenuItems[1].Command.Execute(null);
        phase.SelectCommand.Execute(null);

        Assert.That(_views.Edited, Is.EqualTo(new[] { "Add:1" }));
        Assert.That((view.Properties.Heading, view.Properties.StatusText), Is.EqualTo(("Phase 1: Evidence", "0 of 2 objectives done")));
        Assert.That(view.Properties.Rows.Select(row => (row.Label, row.Value)), Is.EqualTo(new[]
        {
            ("Exit gate", "none written"), ("Objectives", "1.0 A verifier card; 1.1 Checks shown on cards"),
        }));
        Assert.That(view.Properties.Buttons.Select(button => button.Label), Is.EqualTo(new[] { "Add objective…" }));
    }

    [Test]
    public void APiece_StillShowsItsObjectivesCriteria_InProperties()
    {
        var view = Objectives(_views.Theseus());

        view.Select(Objective(view, "1.1").Pieces[0]);

        Assert.That((view.Properties.HasObjective, view.Properties.ObjectiveHeading), Is.EqualTo((true, "Objective 1.1: Checks shown on cards")));
    }

    [Test]
    public void TheUnlistedHeading_SaysWhatItHolds_AndOffersNoEdit()
    {
        var view = Objectives(_views.BuildYourMarket());
        var unlisted = view.Unlisted.Single();

        view.Select(unlisted);

        Assert.That((view.Properties.Heading, view.Properties.HasObjective), Is.EqualTo(("Not in project.md", false)));
        Assert.That(unlisted.MenuItems.Select(item => item.Label), Is.EqualTo(new[] { "Properties" }));
        Assert.That(view.Properties.Rows.First().Value, Is.EqualTo($"{RedirectAnchoring}, {ClientFixes}"));
    }

    [Test]
    public void ARebuild_KeepsAHeadingSelected()
    {
        var view = Objectives(_views.Theseus());
        view.Select(Phase(view, 1));

        var rebuilt = _views.Theseus(previous: view);

        Assert.That(rebuilt.Selection, Is.SameAs(Phase(rebuilt, 1)));
        Assert.That(Phase(rebuilt, 1).IsSelected, Is.True);
    }

    // The edits as the shell wires them, over a shell's EditProject: the file as last read, a refusal said and returning
    // false. Redraws counts how often a refusal asked for the view to be drawn again.
    private ProjectViewEdits FileEdits(List<string> notices, Func<NewObjective?>? ask = null, string? title = null) =>
        ProjectViewFileEdits.For(TheseusProject,
            (project, edit, done) =>
            {
                try
                {
                    ProjectFileWriter.Apply(Read(_folder, project), edit);
                    notices.Add(done);

                    return true;
                }
                catch (ProjectEditRefusedException refused)
                {
                    notices.Add($"{project}: {refused.Message}");

                    return false;
                }
            },
            ask ?? (() => null),
            (_, _, current) => title ?? current,
            () => Redraws++);

    private int Redraws { get; set; }

    // Theseus bootstrap's view with its edits wired as the shell wires them.
    private ProjectViewModel TheseusWithFileEdits(ProjectViewEdits edits)
    {
        var reading = WriteTheseus(_folder);
        IReadOnlyList<PieceCard> cards = [Card(Git22, SessionLifecycle.Idle)];

        return ProjectViewModel.Build(reading, PiecesOfWork.Build(reading, _ => null, cards, []), LinksOn([], cards),
            _views.Tabs(TheseusProject), _views.Settings, _views.Actions, null, edits);
    }

    private string ProjectText => File.ReadAllText(Path.Combine(_folder.Home(TheseusProject), ProjectCatalog.ProjectFileName));

    [Test]
    public void ATick_AfterALineWasAddedAboveSinceTheViewWasDrawn_StillChangesTheRightCriterion()
    {
        var notices = new List<string>();
        var view = Objectives(TheseusWithFileEdits(FileEdits(notices)));
        var heading = Objective(view, "1.0");
        var prose = ProjectText.Replace("## Phase 0:", "A line of prose the owner added.\n\n## Phase 0:", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_folder.Home(TheseusProject), ProjectCatalog.ProjectFileName), prose);

        heading.Criteria.Single().ToggleCommand!.Execute(null);

        var file = Read(_folder, TheseusProject).File!;
        Assert.That(file.Objectives.Single(objective => objective.Number == "1.0").Criteria.Single().IsTicked, Is.True);
        Assert.That(file.Objectives.Where(objective => objective.Number != "1.0").SelectMany(objective => objective.Criteria),
            Has.None.Matches<ProjectCriterion>(criterion => criterion.IsTicked), "no other criterion changed");
        Assert.That((notices.Single(), Redraws), Is.EqualTo(($"Ticked a criterion of {TheseusProject}.", 0)));
    }

    [Test]
    public void ATick_OnACriterionTheFileNoLongerHasAsShown_IsRefused_AndTheViewIsDrawnAgain()
    {
        var notices = new List<string>();
        var view = Objectives(TheseusWithFileEdits(FileEdits(notices)));
        var changed = ProjectText.Replace("### Objective 1.0: A verifier card\n- [ ] A criterion.",
            "### Objective 1.0: A verifier card\n- [ ] A criterion, reworded.", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_folder.Home(TheseusProject), ProjectCatalog.ProjectFileName), changed);

        Objective(view, "1.0").Criteria.Single().ToggleCommand!.Execute(null);

        Assert.That(ProjectText, Is.EqualTo(changed), "nothing written");
        Assert.That(notices.Single(), Does.Contain("criterion 1 of objective 1.0 no longer reads \"A criterion.\""));
        Assert.That(Redraws, Is.EqualTo(1));
    }

    [Test]
    public void EditObjective_OnAnObjectiveRenumberedSinceTheViewWasDrawn_IsRefused_AndTheViewIsDrawnAgain()
    {
        var notices = new List<string>();
        var view = Objectives(TheseusWithFileEdits(FileEdits(notices, title: "Renamed")));
        var renamed = ProjectText.Replace("### Objective 1.0: A verifier card", "### Objective 1.0: Something else", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_folder.Home(TheseusProject), ProjectCatalog.ProjectFileName), renamed);

        Objective(view, "1.0").EditObjectiveCommand!.Execute(null);

        Assert.That(ProjectText, Is.EqualTo(renamed));
        Assert.That(notices.Single(), Does.Contain("objective 1.0 is now \"Something else\", not \"A verifier card\""));
        Assert.That(Redraws, Is.EqualTo(1));
    }

    [Test]
    public void EditObjective_AsShown_RenamesIt()
    {
        var notices = new List<string>();
        var view = Objectives(TheseusWithFileEdits(FileEdits(notices, title: "Renamed")));

        Objective(view, "1.0").EditObjectiveCommand!.Execute(null);

        Assert.That(Read(_folder, TheseusProject).File!.Objectives.Single(objective => objective.Number == "1.0").Title, Is.EqualTo("Renamed"));
        Assert.That(Redraws, Is.Zero);
    }

    [Test]
    public void CollapseAll_ThenOpenWhatNeedsAttentionInTheFlow_KeepsTheHeadingsFolded_AcrossARebuild()
    {
        var view = _views.Theseus();
        view.CollapseAllCommand.Execute(null);

        view.OpenWhatNeedsAttentionCommand.Execute(null);
        var rebuilt = Objectives(_views.Theseus());

        Assert.That(rebuilt.Phases.Select(phase => phase.IsOpen).Concat(rebuilt.AllObjectives.Select(objective => objective.IsOpen)), Has.All.False);
        Assert.That(rebuilt.AllNodes.Where(node => node.IsOpen).Select(node => node.Name),
            Is.EqualTo(new[] { "project-view", "project-home-folders", "projects-and-method-ui" }));
    }

    [Test]
    public void SwitchingToTheFlow_ClearsASelectedHeading_ButKeepsASelectedPiece()
    {
        var view = Objectives(_views.Theseus());
        view.Select(Phase(view, 1));

        view.IsGroupedByFlow = true;

        Assert.That((view.Selection, Phase(view, 1).IsSelected, view.Properties.IsEmpty), Is.EqualTo((null as object, false, true)));

        view.IsGroupedByObjectives = true;
        var box = view.AllNodes.First();
        view.Select(box);
        view.IsGroupedByFlow = true;

        Assert.That(view.Selection, Is.SameAs(box));
    }

    [Test]
    public void AddObjective_FromAPhaseHeading_WritesItAtTheEndOfThatPhase_AsTheShellWiresIt()
    {
        WriteTheseus(_folder);
        var notices = new List<string>();
        var edits = FileEdits(notices, () => new NewObjective("Group by", ["Flow or Objectives.", "Kept per project."]));
        var view = TheseusWithFileEdits(edits);

        Phase(Objectives(view), 0).MenuItems.Single(item => item.Label == "Add objective…").Command.Execute(null);

        var file = Read(_folder, TheseusProject).File!;

        Assert.That(file.Phases[0].Objectives.Select(objective => (objective.Number, objective.Title)).Last(), Is.EqualTo(("0.4", "Group by")));
        Assert.That(file.Phases[0].Objectives.Last().Criteria.Select(criterion => criterion.Text),
            Is.EqualTo(new[] { "Flow or Objectives.", "Kept per project." }));
        Assert.That(file.Phases[1].Objectives.Select(objective => objective.Number), Is.EqualTo(new[] { "1.0", "1.1" }), "phase 1 is untouched");
        Assert.That(notices, Is.EqualTo(new[] { $"Added objective Group by to phase 0 of {TheseusProject}." }));
    }
}
