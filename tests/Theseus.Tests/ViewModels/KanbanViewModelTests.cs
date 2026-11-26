using Theseus.App.ViewModels;
using Theseus.Core.Kanban;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The projects kanban over the project view's own fixtures (projects-kanban spec AC 3 to 6, 8, 11, 14, 16, 19 to 21, 24):
/// a row per project, one card per box in the column of its stage, and its layout remembered.
/// </summary>
[TestFixture]
public class KanbanViewModelTests
{
    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;
    private List<string> _opened = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
        _opened = [];
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private KanbanViewModel Kanban(params KanbanProjectSource[] sources) =>
        KanbanViewModel.Build(sources, _views.Settings, _ => null, name => _opened.Add($"project:{name}"),
            (project, piece) => _opened.Add($"piece:{project}/{piece}"));

    private KanbanProjectSource Theseus(int waiting = 0, string? putAway = null) =>
        new(ProjectViewFixtures.TheseusProject, "Theseus", _views.Theseus()) { WaitingCount = waiting, PutAway = putAway };

    private KanbanProjectSource BuildYourMarket() => new(ProjectViewFixtures.BuildYourMarket, "BuildYourMarket MCP", _views.BuildYourMarket());

    [Test]
    public void ThereIsARowPerProject_InTheOrderGiven()
    {
        var kanban = Kanban(Theseus(), BuildYourMarket());

        Assert.That(kanban.Rows.Select(row => row.Title), Is.EqualTo(new[] { "Theseus", "BuildYourMarket MCP" }));
    }

    [Test]
    public void APutAwayProject_IsLeftOut_UntilClosedProjectsAreShown()
    {
        Assert.That(Kanban(Theseus(putAway: "closed"), BuildYourMarket()).Rows.Select(row => row.Title), Is.EqualTo(new[] { "BuildYourMarket MCP" }));

        Kanban().SetShowsClosed(true);
        var shown = Kanban(Theseus(putAway: "closed"), BuildYourMarket());

        Assert.That(shown.Rows.Select(row => row.Title), Is.EqualTo(new[] { "BuildYourMarket MCP", "Theseus" }));
        Assert.That(shown.Rows[1].IsDimmed, Is.True);
        Assert.That(shown.Rows[1].PutAwayText, Is.EqualTo("closed"));
    }

    [Test]
    public void EveryBox_IsOneCard_InTheColumnOfItsStage()
    {
        var source = BuildYourMarket();
        var kanban = Kanban(source);

        Assert.That(kanban.Cards.Select(card => card.Box), Is.EquivalentTo(source.View!.AllNodes));
        Assert.That(kanban.Cards.All(card => card.Column == KanbanPlacement.ColumnOf(card.Box.Piece)), Is.True);
        Assert.That(kanban.Rows[0].Cells.Select(cell => cell.Column.Column), Is.EqualTo(KanbanColumns.All));
    }

    [Test]
    public void APieceWaitingOnItsSpec_IsInSpec_AndItsButtonIsAcceptSpec()
    {
        var card = Kanban(Theseus()).Cards.Single(each => each.Box.Name == "project-view");

        Assert.That(card.Column, Is.EqualTo(KanbanColumn.Spec));
        Assert.That(card.PrimaryAction?.Label, Does.StartWith("Accept spec"));
        Assert.That(card.MenuEntries.Count, Is.EqualTo(card.Box.Actions.Count));
    }

    [Test]
    public void ACardsButton_RunsTheProjectViewsAction()
    {
        var card = Kanban(Theseus()).Cards.Single(each => each.Box.Name == "project-view");

        card.PrimaryAction!.Command.Execute(null);

        Assert.That(_views.Ran, Is.EqualTo(new[] { "AcceptSpec:project-view" }));
    }

    [Test]
    public void OpeningACard_OpensItsProjectWithThePiece_AndARowHeaderOpensTheProject()
    {
        var kanban = Kanban(Theseus());

        kanban.Cards.Single(each => each.Box.Name == "project-view").OpenCommand.Execute(null);
        kanban.Rows[0].OpenCommand.Execute(null);

        Assert.That(_opened, Is.EqualTo(new[] { $"piece:{ProjectViewFixtures.TheseusProject}/project-view", $"project:{ProjectViewFixtures.TheseusProject}" }));
    }

    [Test]
    public void InACell_WaitingForYouComesFirst()
    {
        foreach (var cell in Kanban(Theseus(), BuildYourMarket()).Rows.SelectMany(row => row.Cells).Where(cell => cell.Cards.Count > 1))
        {
            var waiting = cell.Cards.Select((card, index) => (card, index)).Where(entry => entry.card.State == StageNodeState.WaitingOnYou)
                .Select(entry => entry.index).ToList();

            Assert.That(waiting, Is.EqualTo(Enumerable.Range(0, waiting.Count)), cell.AutomationName);
        }
    }

    [Test]
    public void TheRowHeader_SaysItsWaitsAndCounts()
    {
        var row = Kanban(Theseus(waiting: 4)).Rows[0];
        var done = row.Cells.Single(cell => cell.Column.Column == KanbanColumn.Done).Cards.Count;

        Assert.That(row.WaitsText, Is.EqualTo("◆ 4 waiting"));
        Assert.That(row.CountsText, Is.EqualTo($"{row.Cards.Count() - done} open · {done} done"));
        Assert.That(row.AutomationName, Does.Contain("4 waiting for you"));
    }

    [Test]
    public void AColumnsCount_IsItsCardsAcrossEveryRow()
    {
        var kanban = Kanban(Theseus(), BuildYourMarket());

        foreach (var column in kanban.Columns)
        {
            Assert.That(column.Count, Is.EqualTo(kanban.Cards.Count(card => card.Column == column.Column)), column.Name);
        }
    }

    [Test]
    public void DoneStartsCollapsed_AndACollapseOrResize_IsRememberedNextRun()
    {
        var kanban = Kanban(Theseus());

        Assert.That(kanban.Columns.Single(column => column.Column == KanbanColumn.Done).IsCollapsed, Is.True);

        kanban.Columns.Single(column => column.Column == KanbanColumn.Done).ToggleCommand.Execute(null);
        kanban.Columns.Single(column => column.Column == KanbanColumn.Plan).Resize(300);
        kanban.Columns.Single(column => column.Column == KanbanColumn.Review).IsCollapsed = true;

        var again = Kanban(Theseus());

        Assert.That(again.Columns.Where(column => column.IsCollapsed).Select(column => column.Column), Is.EqualTo(new[] { KanbanColumn.Review }));
        Assert.That(again.Columns.Single(column => column.Column == KanbanColumn.Plan).Width, Is.EqualTo(300));
    }

    [Test]
    public void FitAll_FitsOnlyOpenColumns_WithinTheBounds()
    {
        var kanban = Kanban(Theseus());

        kanban.FitAll(_ => 9000);

        Assert.That(kanban.Columns.Where(column => column.IsOpen).Select(column => column.Width), Is.All.EqualTo(KanbanLayout.MaxWidth));
        Assert.That(kanban.Columns.Single(column => column.Column == KanbanColumn.Done).Width, Is.EqualTo(KanbanLayout.CollapsedWidth));
    }

    [Test]
    public void AFoldedRow_IsRememberedNextRun()
    {
        Kanban(Theseus(), BuildYourMarket()).Rows[1].ToggleCommand.Execute(null);

        var again = Kanban(Theseus(), BuildYourMarket());

        Assert.That(again.Rows.Select(row => row.IsFolded), Is.EqualTo(new[] { false, true }));
    }

    [Test]
    public void FoldAll_FoldsEveryRow()
    {
        var kanban = Kanban(Theseus(), BuildYourMarket());

        kanban.FoldAllCommand.Execute(null);

        Assert.That(kanban.Rows.All(row => row.IsFolded), Is.True);
    }

    [Test]
    public void TheSignature_IsTheSame_WhenNothingChanged_AndMovesWhenAWaitDoes()
    {
        var first = Kanban(Theseus(), BuildYourMarket()).Signature;

        Assert.That(Kanban(Theseus(), BuildYourMarket()).Signature, Is.EqualTo(first));
        Assert.That(Kanban(Theseus(waiting: 2), BuildYourMarket()).Signature, Is.Not.EqualTo(first));
    }

    [Test]
    public void ACard_IsNamedForScreenReaders_WithItsStateColumnAndProject()
    {
        var card = Kanban(Theseus()).Cards.Single(each => each.Box.Name == "project-view");

        Assert.That(card.AutomationName, Is.EqualTo($"{card.Title}, Waiting for you, in Spec, Theseus"));
    }

    [Test]
    public void AProjectThatDoesNotRead_HasARowThatSaysWhy_AndNoCards()
    {
        var row = Kanban(new KanbanProjectSource("broken", "broken", null) { Problem = "project.md does not read" }).Rows[0];

        Assert.That(row.CountsText, Is.EqualTo("project.md does not read"));
        Assert.That(row.Cards, Is.Empty);
        Assert.That(row.OpenCommand.CanExecute(null), Is.False);
    }
}
