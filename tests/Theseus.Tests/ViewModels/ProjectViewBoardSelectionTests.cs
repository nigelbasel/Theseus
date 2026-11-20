using Theseus.App.ViewModels;
using Theseus.Tests.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// What opening and closing a project view does to the board (1.1d AC 18, 32), through <see cref="ProjectViewHost"/>,
/// which ShellViewModel.OpenProject and CloseProject call: select a card, open a project, go back, and nothing is
/// selected; while the view is open nothing can be selected to start; and Esc clears the view's selection first.
/// </summary>
[TestFixture]
public class ProjectViewBoardSelectionTests
{
    private static BoardFixture Board()
    {
        var fixture = new BoardFixture().With("git-22").With("ad-hoc");
        fixture.Board.Refresh();

        return fixture;
    }

    [Test]
    public void ACardSelected_ThenAProjectOpened_ThenBack_LeavesNothingSelected()
    {
        var fixture = Board();
        var host = new ProjectViewHost(fixture.Board);
        fixture.Card("git-22")!.IsSelected = true;

        host.Opened();
        Assert.That(fixture.Board.ChosenCards, Is.Empty, "while the project is open");

        host.Closed();
        Assert.That(fixture.Board.ChosenCards, Is.Empty, "after Alt+Left");
        Assert.That(fixture.Board.Cards, Has.None.Matches<WorkstreamCardViewModel>(card => card.IsSelected));
        Assert.That(fixture.Board.SelectionOrder, Is.Null, "the grid's cards are shown again");
    }

    [Test]
    public void WhileAProjectIsOpen_SelectAll_ChoosesNothing_SoStartSelectedHasNothingToStart()
    {
        var fixture = Board();
        var host = new ProjectViewHost(fixture.Board);

        host.Opened();
        fixture.Board.ChooseShown(true);

        Assert.That(fixture.Board.ChosenCards, Is.Empty, "StartSelectedCommand starts the chosen cards, and can run only with some");
        Assert.That(fixture.Board.ShownCards, Is.Empty);
    }

    [Test]
    public void Esc_ClearsTheViewsSelectionFirst_WhereverFocusIs()
    {
        using var folder = TestProjectsFolder.Create();
        var view = new ProjectViews(folder).Theseus();
        var fixture = Board();
        var host = new ProjectViewHost(fixture.Board);
        host.Opened();

        Assert.That(host.CanClearSelection(view), Is.False);
        Assert.That(ProjectViewHost.ClearViewSelection(view), Is.False, "nothing in the view: the board's clearing runs");

        view.Select(view.Roots[0]);

        Assert.That(host.CanClearSelection(view), Is.True);
        Assert.That(ProjectViewHost.ClearViewSelection(view), Is.True);
        Assert.That(view.Selection, Is.Null);
    }

    [Test]
    public void Esc_OnTheBoard_StillHasTheBoardsSelectionToClear()
    {
        var fixture = Board();
        var host = new ProjectViewHost(fixture.Board);
        fixture.Card("ad-hoc")!.IsSelected = true;

        Assert.That(host.CanClearSelection(null), Is.True);
        Assert.That(ProjectViewHost.ClearViewSelection(null), Is.False);
    }
}
