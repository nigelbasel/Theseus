using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Settings;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A project card filled as the shell fills it, from real pieces of work, sits by its cards on a derived axis (1.1e AC 4;
/// review of PR 1, finding 1): the wiring in <see cref="ProjectCardFace"/>, not a hand-set property.
/// </summary>
[TestFixture]
public class ProjectCardPlacementTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private static ColumnViewModel CellOf(BoardFixture fixture, ProjectCardViewModel project) =>
        fixture.Board.Cells.Single(cell => cell.Projects.Contains(project));

    [Test]
    public void AFilledCard_WhoseCardWaitsForAnAnswer_SitsInNeedsYou_AndInItsFirstCardsFolderRow()
    {
        var fixture = new BoardFixture().With(Git22, folder: @"<root>\Theseus").With("ad-hoc", folder: @"<root>\Other");
        fixture.Board.Refresh();
        var project = new ProjectCardViewModel(TheseusProject, "Theseus bootstrap");
        ProjectCardFace.Fill(project, TheseusPieces(_folder, SessionLifecycle.NeedsAttention));
        fixture.Board.ShowProjects([project]);
        fixture.Board.ColumnsBy = BoardColumnsBy.SessionState;

        Assert.That(CellOf(fixture, project).Column, Is.EqualTo(BoardView.NeedsYou));

        fixture.Board.RowsBy = BoardRowsBy.Project;

        Assert.That(CellOf(fixture, project).Area, Is.EqualTo("Theseus"));
    }

    [Test]
    public void ADimmedCard_SitsInNotRunning_WithNoFirstCard()
    {
        var project = new ProjectCardViewModel(TheseusProject, "Theseus bootstrap");
        ProjectCardFace.Fill(project, TheseusPieces(_folder, SessionLifecycle.Running));

        ProjectCardFace.Dim(project);

        Assert.That((project.StateColumn, project.FirstCardKey), Is.EqualTo((BoardView.NotRunning, (string?)null)));
    }
}
