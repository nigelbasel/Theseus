namespace Theseus.Tests.ViewModels;

/// <summary>
/// Selecting cards the Windows way (Nigel, 2026-10-30): click, Ctrl+click, Shift+click and a rubber
/// band, with no tick boxes.
/// </summary>
[TestFixture]
public class BoardSelectionTests
{
    private static BoardFixture Board()
    {
        var fixture = new BoardFixture().With("a").With("b").With("c").With("d", "Done");
        fixture.Board.Refresh();

        return fixture;
    }

    [Test]
    public void Click_SelectsThatCardAlone()
    {
        var fixture = Board();
        fixture.Card("a")!.IsSelected = true;

        fixture.Board.ClickSelect(fixture.Card("b")!, toggle: false, extend: false);

        Assert.That(Selected(fixture), Is.EqualTo(new[] { "b" }));
    }

    [Test]
    public void CtrlClick_AddsAndRemoves()
    {
        var fixture = Board();

        fixture.Board.ClickSelect(fixture.Card("a")!, toggle: false, extend: false);
        fixture.Board.ClickSelect(fixture.Card("c")!, toggle: true, extend: false);

        Assert.That(Selected(fixture), Is.EqualTo(new[] { "a", "c" }));

        fixture.Board.ClickSelect(fixture.Card("a")!, toggle: true, extend: false);

        Assert.That(Selected(fixture), Is.EqualTo(new[] { "c" }));
    }

    [Test]
    public void ShiftClick_SelectsTheRunInReadingOrder()
    {
        var fixture = Board();

        fixture.Board.ClickSelect(fixture.Card("b")!, toggle: false, extend: false);
        fixture.Board.ClickSelect(fixture.Card("d")!, toggle: false, extend: true);

        Assert.That(Selected(fixture), Is.EqualTo(new[] { "b", "c", "d" }));
    }

    [Test]
    public void ShiftClick_SkipsAFoldedStage()
    {
        var fixture = Board();
        fixture.Board.ToggleStage(fixture.Board.StageHeaders.Single(header => header.Stage == "Done"));

        fixture.Board.ClickSelect(fixture.Card("a")!, toggle: false, extend: false);
        fixture.Board.ClickSelect(fixture.Card("c")!, toggle: false, extend: true);

        Assert.That(Selected(fixture), Is.EqualTo(new[] { "a", "b", "c" }));
    }

    [Test]
    public void BandSelect_ReplacesOrAdds()
    {
        var fixture = Board();
        fixture.Card("a")!.IsSelected = true;

        fixture.Board.BandSelect([fixture.Card("c")!], add: true);

        Assert.That(Selected(fixture), Is.EqualTo(new[] { "a", "c" }));

        fixture.Board.BandSelect([fixture.Card("d")!], add: false);

        Assert.That(Selected(fixture), Is.EqualTo(new[] { "d" }));
    }

    [Test]
    public void ClearSelection_UnselectsEverything()
    {
        var fixture = Board();
        fixture.Board.BandSelect([fixture.Card("a")!, fixture.Card("b")!], add: false);

        fixture.Board.ClearSelection();

        Assert.That(Selected(fixture), Is.Empty);
    }

    private static IReadOnlyList<string> Selected(BoardFixture fixture) =>
        [.. fixture.Board.Cards.Where(card => card.IsSelected).Select(card => card.Key).Order()];
}
