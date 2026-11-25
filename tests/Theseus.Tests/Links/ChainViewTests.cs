using Theseus.Core.Domain;
using Theseus.Core.Finding;
using Theseus.Core.Links;

namespace Theseus.Tests.Links;

/// <summary>
/// What the Chains panel shows for a search and its filters (chains-panel-at-scale criteria 3, 8, 9 and 15): a match keeps
/// its ancestors as context, filters combine, Hide completed hides only whole finished chains, and each filter's count is
/// what it would hide on its own.
/// </summary>
[TestFixture]
public class ChainViewTests
{
    private static readonly ChainRowFacts Stopped = new(SessionLifecycle.NotStarted);
    private static readonly ChainRowFacts Done = Stopped with { IsDone = true };
    private static readonly ChainRowFacts Running = new(SessionLifecycle.Running);
    private static readonly ChainRowFacts Asking = new(SessionLifecycle.NeedsAttention);

    private static readonly ChainFilters NoFilters = new(ActiveOnly: false, WaitingOnMe: false, HideCompleted: false);

    [Test]
    public void NoSearchAndNoFilters_ShowsEveryRow()
    {
        var view = ChainView.Apply([Entry("a", Stopped, Entry("b", Done))], SearchQuery.Empty, NoFilters);

        Assert.That(view.Shown, Is.EqualTo(2));
        Assert.That(view.Total, Is.EqualTo(2));
        Assert.That(view.Of("a"), Is.EqualTo(ChainVisibility.Shown));
    }

    [Test]
    public void AMatch_KeepsItsAncestorsAsContext_AndHidesTheRest()
    {
        var view = ChainView.Apply(
            [Entry("lead", Stopped, Entry("middle", Stopped, Entry("needle", Stopped)), Entry("other", Stopped)), Entry("elsewhere", Stopped)],
            SearchQuery.Parse("needle"), NoFilters);

        Assert.Multiple(() =>
        {
            Assert.That(view.Of("needle"), Is.EqualTo(ChainVisibility.Shown));
            Assert.That(view.Of("middle"), Is.EqualTo(ChainVisibility.Context));
            Assert.That(view.Of("lead"), Is.EqualTo(ChainVisibility.Context));
            Assert.That(view.Of("other"), Is.EqualTo(ChainVisibility.Hidden));
            Assert.That(view.Of("elsewhere"), Is.EqualTo(ChainVisibility.Hidden));
            Assert.That(view.Shown, Is.EqualTo(3), "the match and its two ancestors are on screen");
            Assert.That(view.Unfold, Is.EquivalentTo(new[] { "lead", "middle" }));
        });
    }

    [Test]
    public void TheSearch_ReadsEveryFieldOfTheCard()
    {
        var card = new SearchableCard("k-1", "Title", "Proj", "Area", ["ABC-12"], ["tag"], ["nigel/branch"], ["Repo"]);
        var view = ChainView.Apply([new ChainEntry(card, Stopped, [])], SearchQuery.Parse("abc-12 branch"), NoFilters);

        Assert.That(view.Of("k-1"), Is.EqualTo(ChainVisibility.Shown));
    }

    [Test]
    public void ActiveOnly_KeepsActiveCardsAndTheCardsAboveThem()
    {
        var view = ChainView.Apply(
            [Entry("lead", Done, Entry("working", Running), Entry("finished", Done)), Entry("quiet", Stopped)],
            SearchQuery.Empty, NoFilters with { ActiveOnly = true });

        Assert.Multiple(() =>
        {
            Assert.That(view.Of("working"), Is.EqualTo(ChainVisibility.Shown));
            Assert.That(view.Of("lead"), Is.EqualTo(ChainVisibility.Context));
            Assert.That(view.Of("finished"), Is.EqualTo(ChainVisibility.Hidden));
            Assert.That(view.Of("quiet"), Is.EqualTo(ChainVisibility.Hidden));
        });
    }

    [Test]
    public void WaitingOnMe_KeepsOnlyWhatWaitsOnTheOwner()
    {
        var view = ChainView.Apply(
            [Entry("lead", Running, Entry("asking", Asking), Entry("reported", Stopped with { ReportWaiting = true }), Entry("busy", Running))],
            SearchQuery.Empty, NoFilters with { WaitingOnMe = true });

        Assert.Multiple(() =>
        {
            Assert.That(view.Of("asking"), Is.EqualTo(ChainVisibility.Shown));
            Assert.That(view.Of("reported"), Is.EqualTo(ChainVisibility.Shown));
            Assert.That(view.Of("busy"), Is.EqualTo(ChainVisibility.Hidden));
            Assert.That(view.Of("lead"), Is.EqualTo(ChainVisibility.Context));
        });
    }

    [Test]
    public void HideCompleted_HidesAWholeChainOnceEveryCardInItIsDoneOrArchived()
    {
        var view = ChainView.Apply(
            [Entry("finished", Done, Entry("put-away", Stopped with { IsArchived = true })), Entry("half", Done, Entry("todo", Stopped))],
            SearchQuery.Empty, NoFilters with { HideCompleted = true });

        Assert.Multiple(() =>
        {
            Assert.That(view.Of("finished"), Is.EqualTo(ChainVisibility.Hidden));
            Assert.That(view.Of("put-away"), Is.EqualTo(ChainVisibility.Hidden));
            Assert.That(view.Of("half"), Is.EqualTo(ChainVisibility.Shown), "a finished card in an unfinished chain stays");
            Assert.That(view.Of("todo"), Is.EqualTo(ChainVisibility.Shown), "merely stopped is not completed");
        });
    }

    [Test]
    public void HideCompleted_KeepsAFinishedChainWithAReportWaiting()
    {
        var view = ChainView.Apply([Entry("finished", Done, Entry("waiting", Done with { ReportWaiting = true }))],
            SearchQuery.Empty, NoFilters with { HideCompleted = true });

        Assert.That(view.Of("finished"), Is.EqualTo(ChainVisibility.Shown));
    }

    [Test]
    public void FiltersAndSearch_Combine_ARowShowsOnlyIfEveryOneKeepsIt()
    {
        var view = ChainView.Apply(
            [Entry("lead", Stopped, Entry("needle running", Running), Entry("needle stopped", Stopped))],
            SearchQuery.Parse("needle"), NoFilters with { ActiveOnly = true });

        Assert.That(view.Of("needle-running"), Is.EqualTo(ChainVisibility.Shown));
        Assert.That(view.Of("needle-stopped"), Is.EqualTo(ChainVisibility.Hidden));
    }

    [Test]
    public void EachFiltersCount_IsWhatItHidesOnItsOwn()
    {
        var chains = new[]
        {
            Entry("finished", Done, Entry("finished-too", Done)),
            Entry("lead", Stopped, Entry("working", Running), Entry("asking", Asking)),
        };

        var view = ChainView.Apply(chains, SearchQuery.Empty, new ChainFilters(ActiveOnly: true, WaitingOnMe: false, HideCompleted: true));

        Assert.Multiple(() =>
        {
            Assert.That(view.Total, Is.EqualTo(5));
            Assert.That(view.HiddenByHideCompleted, Is.EqualTo(2));
            Assert.That(view.HiddenByActiveOnly, Is.EqualTo(2), "finished and finished-too; lead stays as context");
            Assert.That(view.HiddenByWaitingOnMe, Is.EqualTo(3), "counted even while it is off");
            Assert.That(view.Shown, Is.EqualTo(3));
        });
    }

    [Test]
    public void ARowWhoseCardsBelowAreAllCompleted_IsAFinishedSubChain()
    {
        var finished = Entry("lead", Running, Entry("a", Done, Entry("b", Stopped with { IsArchived = true })));
        var unfinished = Entry("lead2", Done, Entry("c", Done), Entry("d", Stopped));

        Assert.Multiple(() =>
        {
            Assert.That(ChainView.IsFinishedBelow(finished), Is.True, "everything below lead is finished, though lead runs");
            Assert.That(ChainView.IsFinishedBelow(unfinished), Is.False);
            Assert.That(ChainView.IsFinishedBelow(Entry("leaf", Done)), Is.False, "a row with nothing below has nothing to fold");
        });
    }

    private static ChainEntry Entry(string title, ChainRowFacts facts, params ChainEntry[] children) =>
        new(new SearchableCard(title.Replace(' ', '-'), title, string.Empty, string.Empty, [], [], [], []), facts, children);
}
