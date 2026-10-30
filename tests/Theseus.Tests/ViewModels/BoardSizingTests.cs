using Theseus.App.ViewModels;
using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

/// <summary>Columns and rows can be resized, and keep their sizes per view across a restart (Nigel, 2026-10-30).</summary>
[TestFixture]
public class BoardSizingTests
{
    [Test]
    public void ResizeColumns_TradesWidthBetweenTheTwoAndLeavesTheRest()
    {
        var fixture = new BoardFixture();
        var (first, second, third) = Headers(fixture);

        fixture.Board.ResizeColumns(first, 0.5);

        Assert.Multiple(() =>
        {
            Assert.That(first.Weight, Is.EqualTo(1.5).Within(0.001));
            Assert.That(second.Weight, Is.EqualTo(0.5).Within(0.001));
            Assert.That(third.Weight, Is.EqualTo(1).Within(0.001));
            Assert.That(fixture.Board.Cells.Where(cell => cell.Column == first.Stage).Select(cell => cell.Weight),
                Is.All.EqualTo(1.5).Within(0.001), "every row's cell keeps in line with its heading");
        });
    }

    [Test]
    public void ResizeColumns_NeverShrinksAColumnBelowAFifth()
    {
        var fixture = new BoardFixture();
        var (first, second, _) = Headers(fixture);

        fixture.Board.ResizeColumns(first, 5);

        Assert.That(second.Weight, Is.EqualTo(0.2).Within(0.001));
        Assert.That(first.Weight, Is.EqualTo(1.8).Within(0.001));
    }

    [Test]
    public void ResizeColumns_SkipsAFoldedNeighbour()
    {
        var fixture = new BoardFixture();
        var (first, second, third) = Headers(fixture);
        fixture.Board.ToggleStage(second);

        fixture.Board.ResizeColumns(first, 0.25);

        Assert.That((second.Weight, third.Weight), Is.EqualTo((1.0, 0.75)));
    }

    [Test]
    public void Sizes_AreRememberedPerViewAcrossARestart()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();
        var (first, _, _) = Headers(fixture);

        fixture.Board.ResizeColumns(first, 0.5);
        fixture.Board.SetRowHeight(fixture.Board.Rows[0], 300);
        fixture.Board.RememberSizes();

        var reopened = fixture.Reopen();

        Assert.Multiple(() =>
        {
            Assert.That(reopened.StageHeaders[0].Weight, Is.EqualTo(1.5).Within(0.001));
            Assert.That(reopened.Rows[0].Height, Is.EqualTo(300));
            Assert.That(fixture.Settings.ColumnWeights.Keys, Does.Contain("Stage:New"));
        });

        fixture.Board.ColumnsBy = BoardColumnsBy.SessionState;

        Assert.That(fixture.Board.StageHeaders.Select(header => header.Weight), Is.All.EqualTo(1),
            "another view has its own sizes");
    }

    [Test]
    public void Reset_PutsColumnsBackToEvenAndARowBackToFitting()
    {
        var fixture = new BoardFixture();
        var (first, _, _) = Headers(fixture);
        fixture.Board.ResizeColumns(first, 0.5);
        fixture.Board.SetRowHeight(fixture.Board.Rows[0], 50);

        Assert.That(fixture.Board.Rows[0].Height, Is.EqualTo(BoardViewModel.MinimumRowHeight), "a row is never dragged to nothing");

        fixture.Board.ResetColumnWidths();
        fixture.Board.ResetRowHeight(fixture.Board.Rows[0]);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.StageHeaders.Select(header => header.Weight), Is.All.EqualTo(1));
            Assert.That(double.IsNaN(fixture.Board.Rows[0].Height), Is.True);
            Assert.That(fixture.Settings.ColumnWeights, Is.Empty);
            Assert.That(fixture.Settings.RowHeights, Is.Empty);
        });
    }

    private static (StageHeaderViewModel, StageHeaderViewModel, StageHeaderViewModel) Headers(BoardFixture fixture) =>
        (fixture.Board.StageHeaders[0], fixture.Board.StageHeaders[1], fixture.Board.StageHeaders[2]);
}
