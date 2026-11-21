using System.Windows;
using System.Windows.Controls;
using Theseus.App.Services;
using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A press, drag and drop on the board (1.1d AC 41, 42). Dragging a project card failed for two reasons, found before
/// the fix and kept here as tests: a press on a project card armed the rubber band, which only knew workstream cards,
/// so the band took the mouse whenever the pointer left the card before the drag began, and a click on the card cleared
/// the selection; and while the columns show session state, where nothing can be dropped, the drop was refused
/// without a word.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class BoardGestureTests
{
    private static TextBlock TextOn(object item)
    {
        var text = new TextBlock { Text = "a title" };
        _ = new Border { DataContext = item, Child = new StackPanel { Children = { text } } };

        return text;
    }

    [Test]
    public void APressOnAProjectCard_StartsNoRubberBand()
    {
        var text = TextOn(new ProjectCardViewModel("theseus", "Theseus bootstrap"));

        Assert.That(BoardGesture.StartsBand(text), Is.False);
    }

    [Test]
    public void APressOnEmptyBoard_StartsARubberBand_AndOnAButtonDoesNot()
    {
        var empty = new Border { DataContext = new ColumnViewModel("New", "Dev") };
        var button = new Button();
        _ = new Border { DataContext = new ColumnViewModel("New", "Dev"), Child = button };

        Assert.That((BoardGesture.StartsBand(empty), BoardGesture.StartsBand(button)), Is.EqualTo((true, false)));
    }

    [Test]
    public void APressOnAProjectCard_IsOnThatProjectCard_WhichADragCarriesByName()
    {
        var project = new ProjectCardViewModel("theseus", "Theseus bootstrap");

        Assert.That(BoardGesture.ItemAt(TextOn(project)), Is.SameAs(project));
        Assert.That(project.DragPayload, Is.EqualTo("project:theseus"));
    }

    [Test]
    public void WhileColumnsShowSessionState_ADropIsRefused_SayingWhy()
    {
        var fixture = new BoardFixture().With("ad-hoc");
        fixture.Board.Refresh();
        fixture.Board.ColumnsBySessionState = true;

        Assert.That(BoardGesture.Refusal(fixture.Board),
            Is.EqualTo("Cards and projects are moved only while the columns show stages: choose View › Columns by › Stage."));

        fixture.Board.ColumnsByStage = true;

        Assert.That(BoardGesture.Refusal(fixture.Board), Is.Null);
    }

    [Test]
    public void AProjectCardDropped_OnAnyCell_GoesThere_AboveItsCards_AndIsKept()
    {
        var fixture = new BoardFixture().With("ad-hoc", column: "In progress", area: "Dev");
        fixture.Board.Refresh();
        var project = new ProjectCardViewModel("theseus", "Theseus bootstrap");
        fixture.Board.ShowProjects([project]);
        var target = fixture.Board.Cells.Single(cell => cell.Column == "In progress" && cell.Area == "Dev");

        BoardGesture.Drop(fixture.Board, project.DragPayload, target, 0);

        Assert.That(target.Projects, Is.EqualTo(new[] { project }));
        Assert.That(target.Cards.Select(card => card.Key), Is.EqualTo(new[] { "ad-hoc" }), "its cards stay, below it");
        Assert.That(fixture.Settings.ProjectCardPlaces["theseus"], Is.EqualTo(new Theseus.Core.Settings.ProjectCardPlace("In progress", "Dev")));
    }

    [Test]
    public void ADragCarriesWhatWasPressed_EvenOnceThePointerHasLeftIt()
    {
        var fixture = new BoardFixture().With("ad-hoc");
        fixture.Board.Refresh();
        var project = new ProjectCardViewModel("theseus", "Theseus bootstrap");
        var press = new BoardPress();

        press.Down(TextOn(project), new Point(10, 10));

        Assert.That(press.Moved(new Point(11, 11), leftPressed: true, projectOpen: false, fixture.Board), Is.EqualTo(BoardDragStep.None),
            "not yet a drag");
        Assert.That(press.Moved(new Point(10, 60), leftPressed: true, projectOpen: false, fixture.Board).Drag, Is.SameAs(project),
            "the move is never asked what it is over now; the old code looked there and dragged nothing");
        Assert.That(press.Moved(new Point(10, 90), leftPressed: true, projectOpen: false, fixture.Board), Is.EqualTo(BoardDragStep.None),
            "one drag per press");
    }

    [Test]
    public void APressOnEmptyBoard_OrInTheProjectView_DragsNothing()
    {
        var fixture = new BoardFixture().With("ad-hoc");
        fixture.Board.Refresh();
        var press = new BoardPress();

        press.Down(new Border(), new Point(0, 0));

        Assert.That(press.Moved(new Point(0, 60), leftPressed: true, projectOpen: false, fixture.Board), Is.EqualTo(BoardDragStep.None));

        press.Down(TextOn(new ProjectCardViewModel("theseus", "Theseus bootstrap")), new Point(0, 0));

        Assert.That(press.Moved(new Point(0, 60), leftPressed: true, projectOpen: true, fixture.Board), Is.EqualTo(BoardDragStep.None));
    }

    [Test]
    public void WhereNothingCanBeDropped_NoDragStarts_SoAWobblingClickStillSelects_AndARealDragIsToldWhyOnce()
    {
        var fixture = new BoardFixture().With("ad-hoc");
        fixture.Board.Refresh();
        fixture.Board.ColumnsBySessionState = true;
        var press = new BoardPress();

        press.Down(TextOn(fixture.Card("ad-hoc")!), new Point(0, 0));

        Assert.That(press.Moved(new Point(0, 6), leftPressed: true, projectOpen: false, fixture.Board), Is.EqualTo(BoardDragStep.None),
            "a wobble says nothing");

        var dragged = press.Moved(new Point(0, 60), leftPressed: true, projectOpen: false, fixture.Board);

        Assert.That((dragged.Drag, dragged.Refusal), Is.EqualTo(((IBoardCard?)null, BoardGesture.Refusal(fixture.Board))));
        Assert.That(press.Moved(new Point(0, 90), leftPressed: true, projectOpen: false, fixture.Board), Is.EqualTo(BoardDragStep.None), "once");
    }
}
