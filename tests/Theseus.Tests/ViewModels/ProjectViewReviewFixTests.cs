using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The project card after the review of 1.1's PR 2: a project card moves only where cards do (item 7). Item 4's tests,
/// of the pipeline grid's sources updated in place, went with the grid (1.1d PR 2).
/// </summary>
[TestFixture]
public class ProjectViewReviewFixTests
{
    [Test]
    public void AProjectCard_IsNotMoved_WhereCardsCannotBeDropped()
    {
        var fixture = new BoardFixture().With("ad-hoc");
        fixture.Board.Refresh();
        var project = new ProjectCardViewModel("p", "P");
        fixture.Board.ShowProjects([project]);
        fixture.Board.ColumnsBySessionState = true;
        fixture.Board.RowsByProject = true;

        fixture.Board.MoveProject("p", "In progress", "Dev");

        Assert.That(fixture.Settings.ProjectCardPlaces, Is.Empty);
    }
}
