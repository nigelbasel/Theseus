using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class BoardSplitTests
{
    // The board this was written for.
    private static readonly string[] Nigels =
        ["New", "Research", "Dev - Primary", "Dev - Secondary", "Side Project", "Done"];

    [Test]
    public void Split_SeparatesStagesFromKindsOfWork()
    {
        var (stages, areas) = BoardSplit.Split(Nigels);

        Assert.Multiple(() =>
        {
            Assert.That(stages, Does.Contain("New"));
            Assert.That(stages, Does.Contain("Done"));
            Assert.That(areas, Does.Contain("Research"));
            Assert.That(areas, Does.Contain("Side Project"));
        });
    }

    [Test]
    public void Split_MergesTwoLanesThatWereNeverTwoKindsOfWork()
    {
        var (_, areas) = BoardSplit.Split(Nigels);

        Assert.That(areas.Count(area => area.StartsWith("Dev", StringComparison.Ordinal)), Is.EqualTo(1),
            "Dev - Primary and Dev - Secondary were one area split by a qualifier");
        Assert.That(areas, Does.Contain("Dev"));
    }

    [Test]
    public void Split_AddsSomewhereForWorkToBeInProgress()
    {
        var (stages, _) = BoardSplit.Split(Nigels);

        Assert.That(stages, Does.Contain(BoardSplit.InProgress),
            "the old board had no in-progress stage: everything in progress was a category");
    }

    [Test]
    public void Split_KeepsTheStagesInTheOrderTheyWereIn()
    {
        var (stages, _) = BoardSplit.Split(Nigels);

        Assert.That(stages.First(), Is.EqualTo("New"));
        Assert.That(stages.Last(), Is.EqualTo("Done"), "the last stage is what a whole-board start skips");
    }

    [Test]
    public void Split_GivesABoardOfOnlyStagesARowToPutCardsIn()
    {
        var (stages, areas) = BoardSplit.Split(["New", "In flight", "Done"]);

        Assert.That(stages, Is.EqualTo(new[] { "New", "In flight", "Done" }));
        Assert.That(areas, Is.EqualTo(BoardAreas.Default), "every card still needs a row");
    }

    [Test]
    public void Split_GivesABoardOfOnlyCategoriesTheStagesItNeeds()
    {
        var (stages, areas) = BoardSplit.Split(["Research", "Dev"]);

        Assert.That(stages, Does.Contain(BoardSplit.InProgress));
        Assert.That(stages, Has.Count.GreaterThan(1));
        Assert.That(areas, Is.EqualTo(new[] { "Research", "Dev" }));
    }

    [Test]
    public void Place_LeavesAStageWhereItWasAndPutsItInTheFirstRow()
    {
        var (stage, area) = BoardSplit.Place("Done", ["Research", "Dev"]);

        Assert.That(stage, Is.EqualTo("Done"));
        Assert.That(area, Is.EqualTo("Research"));
    }

    [Test]
    public void Place_TurnsAKindOfWorkIntoARowOfWorkInProgress()
    {
        var (stage, area) = BoardSplit.Place("Dev - Secondary", ["Research", "Dev"]);

        Assert.That(stage, Is.EqualTo(BoardSplit.InProgress));
        Assert.That(area, Is.EqualTo("Dev"));
    }

    [Test]
    public void IsNeeded_OnlyWhereThereAreLanesAndNoAreasYet()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BoardSplit.IsNeeded(Nigels, []), Is.True);
            Assert.That(BoardSplit.IsNeeded(Nigels, null), Is.True);
            Assert.That(BoardSplit.IsNeeded(Nigels, ["Research"]), Is.False, "already split");
            Assert.That(BoardSplit.IsNeeded([], []), Is.False, "nothing to split");
        });
    }
}
