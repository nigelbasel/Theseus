using Theseus.App.ViewModels;
using Theseus.Tests.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Properties in a narrow window (1.1d AC 52 to 54; Nigel's decision 6, 2026-11-21): below the flow's minimum, the
/// splitter and the pane's minimum the pane is not docked and the status bar says so once; selecting then opens
/// Properties as an overlay that × and Esc close, leaving the selection; with room again it docks.
/// </summary>
[TestFixture]
public class ProjectPaneDockTests
{
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

    private static PieceNodeViewModel Box(ProjectViewModel view, string piece) => view.AllNodes.Single(node => node.Name == piece);

    [TestCase(666, true, Description = "400 px of flow, the 6 px splitter and the 260 px pane")]
    [TestCase(665, false)]
    [TestCase(384, false, Description = "a 720 px window with the Live sessions panel open")]
    [TestCase(944, true, Description = "a 1280 px window with the Live sessions panel open")]
    public void ThePane_DocksOnlyWhereTheFlowThePaneAndTheSplitterAllFit(double viewWidth, bool docks)
    {
        Assert.That(ProjectPaneWidth.DockingWidth, Is.EqualTo(ProjectPaneWidth.MinimumFlow + ProjectPaneWidth.Splitter + ProjectPaneWidth.Minimum));
        Assert.That(ProjectPaneWidth.Docks(viewWidth), Is.EqualTo(docks));
    }

    [Test]
    public void TooNarrow_ThePaneIsNotDocked_AndTheStatusBarSaysSoOnce()
    {
        var view = _views.Theseus();

        view.PaneFits(false);
        view.PaneFits(false);

        Assert.That((view.IsPaneDocked, view.IsOverlayOpen), Is.EqualTo((false, false)));
        Assert.That(_views.Notices, Is.EqualTo(new[] { "Properties hidden: the window is too narrow" }));
    }

    [Test]
    public void ARebuild_WhileTooNarrow_SaysNothingAgain_AndKeepsTheOverlayAsItWas()
    {
        var view = _views.Theseus();
        view.PaneFits(false);
        view.Select(Box(view, "project-view"));

        var rebuilt = _views.Theseus(previous: view);

        Assert.That((rebuilt.IsPaneDocked, rebuilt.IsOverlayOpen), Is.EqualTo((false, true)));
        Assert.That(_views.Notices, Has.Count.EqualTo(1));
    }

    [Test]
    public void WhileNotDocked_SelectingABoxAStageOrAHeading_OpensTheOverlay()
    {
        var view = _views.Theseus();
        view.PaneFits(false);

        view.Select(Box(view, "stop-timing"));
        Assert.That(view.IsOverlayOpen, Is.True);

        view.CloseOverlayCommand.Execute(null);
        view.Select(Box(view, "stop-timing").Stages[0]);
        Assert.That(view.IsOverlayOpen, Is.True);

        view.CloseOverlayCommand.Execute(null);
        view.IsGroupedByObjectives = true;
        view.Phases[0].SelectCommand.Execute(null);
        Assert.That(view.IsOverlayOpen, Is.True);
    }

    [Test]
    public void WhileDocked_SelectingOpensNoOverlay()
    {
        var view = _views.Theseus();

        view.Select(Box(view, "stop-timing"));

        Assert.That((view.IsPaneDocked, view.IsOverlayOpen), Is.EqualTo((true, false)));
    }

    [Test]
    public void TheCloseButton_AndEsc_CloseTheOverlay_LeavingTheSelection_AndASecondEscClearsIt()
    {
        var view = _views.Theseus();
        view.PaneFits(false);
        var box = Box(view, "stop-timing");
        view.Select(box);

        view.CloseOverlayCommand.Execute(null);
        Assert.That((view.IsOverlayOpen, view.Selection), Is.EqualTo((false, box)));

        view.Select(box);
        view.ClearSelectionCommand.Execute(null);
        Assert.That((view.IsOverlayOpen, view.Selection), Is.EqualTo((false, box)), "Esc closes the overlay first");
        Assert.That(view.CloseOverlayCommand.CanExecute(null), Is.False);

        view.ClearSelectionCommand.Execute(null);
        Assert.That(view.Selection, Is.Null);
    }

    [Test]
    public void TheWindowsEsc_ThroughTheHost_ClosesTheOverlayBeforeClearingTheSelection()
    {
        var view = _views.Theseus();
        view.PaneFits(false);
        var box = Box(view, "stop-timing");
        view.Select(box);

        Assert.That(ProjectViewHost.ClearViewSelection(view), Is.True);
        Assert.That((view.IsOverlayOpen, view.Selection), Is.EqualTo((false, box)));
        Assert.That(ProjectViewHost.ClearViewSelection(view), Is.True);
        Assert.That(view.Selection, Is.Null);
        Assert.That(ProjectViewHost.ClearViewSelection(view), Is.False, "then the board's Esc runs");
    }

    [Test]
    public void WithRoomAgain_ThePaneDocks_AndTheOverlayCloses_KeepingTheSelection()
    {
        var view = _views.Theseus();
        view.PaneFits(false);
        var box = Box(view, "stop-timing");
        view.Select(box);

        view.PaneFits(true);

        Assert.That((view.IsPaneDocked, view.IsOverlayOpen, view.Selection), Is.EqualTo((true, false, box)));
        Assert.That(view.Properties.Heading, Is.EqualTo("Five changes through the method by hand"));
    }
}
