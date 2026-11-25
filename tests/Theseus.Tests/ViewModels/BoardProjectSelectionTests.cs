using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A project card is selected as a card is (1.1d AC 41): click, Ctrl+click, Shift+click in reading order (a cell's
/// project cards above its cards), the rubber band, Ctrl+A, Ctrl+I, Select live and Esc; it is unselected when a
/// project view hides it; and a command for one workstream is refused while only projects are selected, and skips them
/// in a mixed selection.
/// </summary>
[TestFixture]
public class BoardProjectSelectionTests
{
    private BoardFixture _fixture = null!;
    private ProjectCardViewModel _project = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = new BoardFixture().With("first").With("second").With("later", column: "In progress");
        _fixture.Board.Refresh();
        _project = new ProjectCardViewModel("theseus", "Theseus bootstrap");
        _fixture.Board.ShowProjects([_project]);
    }

    private BoardViewModel Board => _fixture.Board;

    private WorkstreamCardViewModel Card(string key) => _fixture.Card(key)!;

    [Test]
    public void AClick_SelectsTheProjectAlone_AndCtrlClickAddsACard()
    {
        Card("first").IsSelected = true;

        Board.ClickSelect(_project, toggle: false, extend: false);

        Assert.That((_project.IsSelected, Card("first").IsSelected), Is.EqualTo((true, false)));

        Board.ClickSelect(Card("second"), toggle: true, extend: false);

        Assert.That(Board.ChosenProjects, Is.EqualTo(new[] { _project }));
        Assert.That(Board.ChosenCards.Select(card => card.Key), Is.EqualTo(new[] { "second" }));
    }

    [Test]
    public void ShiftClick_SelectsARunInReadingOrder_WithTheProjectAboveItsCellsCards()
    {
        Assert.That(Board.VisibleItems.Take(3), Is.EqualTo(new IBoardCard[] { _project, Card("first"), Card("second") }));

        Board.ClickSelect(_project, toggle: false, extend: false);
        Board.ClickSelect(Card("second"), toggle: false, extend: true);

        Assert.That((_project.IsSelected, Card("first").IsSelected, Card("second").IsSelected, Card("later").IsSelected),
            Is.EqualTo((true, true, true, false)));
    }

    [Test]
    public void TheRubberBand_SelectsAProjectCardItTouches()
    {
        Board.BandSelect([_project, Card("later")], add: false);

        Assert.That((_project.IsSelected, Card("later").IsSelected, Card("first").IsSelected), Is.EqualTo((true, true, false)));
    }

    [Test]
    public void SelectAll_Invert_AndEsc_ReachTheProjectCard()
    {
        Board.ChooseShown(true);

        Assert.That(_project.IsSelected, Is.True, "Ctrl+A");

        Board.InvertShown();

        Assert.That(Board.ShownItems.Any(item => item.IsSelected), Is.False, "Ctrl+I");

        Board.InvertShown();
        Board.ClearSelection();

        Assert.That(_project.IsSelected, Is.False, "Esc");
    }

    [Test]
    public void SelectAllAndInvert_PassOverAProjectCardInAFoldedRowOrColumn()
    {
        var cell = Board.Cells.Single(each => each.Projects.Contains(_project));
        Board.Rows.Single(row => row.Cells.Contains(cell)).IsCollapsed = true;

        Board.ChooseShown(true);
        Board.InvertShown();
        Board.InvertShown();

        Assert.That(_project.IsSelected, Is.False);

        Board.Rows.Single(row => row.Cells.Contains(cell)).IsCollapsed = false;
        cell.IsCollapsed = true;
        Board.ChooseShown(true);

        Assert.That(_project.IsSelected, Is.False);
    }

    [Test]
    public void SelectLive_ChoosesAProjectWhoseCardHasASession()
    {
        _project.IsLive = true;

        Board.SelectLiveShown();

        Assert.That(Board.ChosenProjects, Is.EqualTo(new[] { _project }));
    }

    [Test]
    public void OpeningAProjectView_UnselectsTheProjectCard_ItHides()
    {
        Board.ClickSelect(_project, toggle: false, extend: false);

        new ProjectViewHost(Board).Opened();

        Assert.That(_project.IsSelected, Is.False);
        Assert.That(Board.ChosenProjects, Is.Empty);
    }

    [Test]
    public void ACommandForOneWorkstream_IsRefusedWithOnlyAProjectSelected_SayingWhy_AndSkipsItBesideACard()
    {
        Board.ClickSelect(_project, toggle: false, extend: false);

        Assert.That(ProjectSelection.OneCard(Board), Is.Null);
        Assert.That(ProjectSelection.ToolTip(Board, "The selected card's properties"),
            Is.EqualTo("Not for a project: open it and select its cards"));

        Board.ClickSelect(Card("first"), toggle: true, extend: false);

        Assert.That(ProjectSelection.OneCard(Board), Is.SameAs(Card("first")));
        Assert.That(ProjectSelection.ToolTip(Board, "The selected card's properties"), Is.EqualTo("The selected card's properties"));
        Assert.That(ProjectSelection.Skipped(Board), Is.EqualTo(" 1 project skipped."));
    }

    [Test]
    public void ACommandOnAMixedSelection_SkipsTheProject_AndSaysSo()
    {
        Assert.That(ProjectSelection.Skipped(Board), Is.Empty);

        Board.BandSelect([_project, Card("first")], add: false);

        Assert.That(ProjectSelection.Skipped(Board), Is.EqualTo(" 1 project skipped."));
        Assert.That(Board.ChosenCards.Select(card => card.Key), Is.EqualTo(new[] { "first" }), "what the command acts on");
    }
}
