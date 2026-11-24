using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A state chip filters the Objectives only lens as it filters Rounds and work (Nigel, 2026-11-24, through git-22: in
/// Objectives only, "● 2 busy" changed nothing): only the objectives and phases holding a matching piece show, open, with
/// only their matching pieces; the filter's words are the same; a second press or Esc shows everything again. Checklist
/// G124 and G161: a control that does nothing silently reads as broken.
/// </summary>
[TestFixture]
public class ObjectivesLensFilterTests
{
    private const StageNodeState Built = StageNodeState.HoldoutsNotRun;

    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void AChip_InTheObjectivesLens_ShowsOnlyTheObjectivesHoldingAMatch_OpenWithOnlyTheMatchingPieces()
    {
        ProjectViewModel view = Objectives();

        view.ToggleFilter(Built, add: false);

        List<ObjectiveHeadingViewModel> objectives = [.. view.Phases.SelectMany(phase => phase.Objectives).Concat(view.Unlisted)];
        Assert.That(objectives.Where(objective => objective.Pieces.Any(Matches)), Is.Not.Empty);
        Assert.That(objectives.Where(objective => objective.Pieces.Any(Matches)), Has.All.Matches<ObjectiveHeadingViewModel>(
            objective => objective.IsShown && objective.ShowsPieces));
        Assert.That(objectives.Where(objective => !objective.Pieces.Any(Matches)).Select(objective => objective.IsShown), Has.None.True);
        Assert.That(objectives.SelectMany(objective => objective.Pieces).Where(node => node.IsShown).Select(Matches), Has.All.True);
        Assert.That(view.Phases.Where(phase => phase.Objectives.Any(objective => objective.IsShown)),
            Has.All.Matches<PhaseHeadingViewModel>(phase => phase.IsShown && phase.IsOpen));
        Assert.That(view.Phases.Where(phase => !phase.Objectives.Any(objective => objective.Pieces.Any(Matches))).Select(phase => phase.IsShown),
            Has.None.True);
    }

    [Test]
    public void TheFiltersWords_AreTheSameInBothLenses()
    {
        ProjectViewModel flow = _views.BuildYourMarket();
        ProjectViewModel objectives = Objectives();

        flow.ToggleFilter(Built, add: false);
        objectives.ToggleFilter(Built, add: false);

        Assert.That(objectives.FilterText, Is.Not.Empty.And.EqualTo(flow.FilterText));
    }

    [Test]
    public void ASecondPress_OrEsc_ShowsEveryObjectiveAgain()
    {
        ProjectViewModel view = Objectives();

        view.ToggleFilter(Built, add: false);
        view.ToggleFilter(Built, add: false);
        Assert.That(AllObjectives(view).Select(objective => objective.IsShown), Has.All.True, "a second press");
        Assert.That(view.Phases.Select(phase => phase.IsShown), Has.All.True);

        view.ToggleFilter(Built, add: false);
        view.ClearSelectionCommand.Execute(null);
        Assert.That(AllObjectives(view).Select(objective => objective.IsShown), Has.All.True, "Esc");
        Assert.That(AllObjectives(view).SelectMany(objective => objective.Pieces).Select(node => node.IsShown), Has.All.True);
    }

    [Test]
    public void EveryChip_CanRunInBothLenses_SoNoneIsASilentNoOp()
    {
        ProjectViewModel view = Objectives();

        Assert.That(view.Counts.Select(chip => chip.Command!.CanExecute(null)), Has.All.True);
    }

    private ProjectViewModel Objectives()
    {
        ProjectViewModel view = _views.BuildYourMarket();
        view.IsGroupedByObjectives = true;

        return view;
    }

    private static IEnumerable<ObjectiveHeadingViewModel> AllObjectives(ProjectViewModel view) =>
        view.Phases.SelectMany(phase => phase.Objectives).Concat(view.Unlisted);

    private static bool Matches(PieceNodeViewModel node) => node.Piece.Status.State == Built;
}
