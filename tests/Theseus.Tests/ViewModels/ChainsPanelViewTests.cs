using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The Chains panel's search, filters, row states, folds and project headers, driven through the view model with no window
/// (chains-panel-at-scale criteria 1 to 19, accepted by Nigel 2026-11-25).
/// </summary>
[TestFixture]
public class ChainsPanelViewTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private LinkBook _book = null!;
    private List<WorkstreamCardViewModel> _cards = null!;
    private MemorySettingsStore _settings = null!;
    private ChainsViewModel _chains = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-chains-panel-{Guid.NewGuid():N}");
        _book = new LinkBook(new LinkStateStore(Path.Combine(_directory, "links.json")), new FixedClock(Nine));
        _cards = [Card("lead"), Card("research"), Card("build"), Card("old-lead"), Card("old-child")];
        _settings = new MemorySettingsStore { Settings = Theseus.Core.Settings.TheseusSettings.Default with { ChainsFilters = new(false, false, false) } };
        _chains = NewChains();

        _book.Add(LinkKind.HandsOver, "lead", "research");
        _book.Add(LinkKind.HandsOver, "research", "build");
        _book.Add(LinkKind.HandsOver, "old-lead", "old-child");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void EveryRow_CarriesAMarkAndAWord()
    {
        Find("build").Lifecycle = SessionLifecycle.Running;
        Find("research").Lifecycle = SessionLifecycle.NeedsAttention;
        MarkDone("old-child");

        _chains.Update();

        Assert.Multiple(() =>
        {
            Assert.That(Row("build").StateText, Is.EqualTo("● running"));
            Assert.That(Row("research").StateText, Is.EqualTo("◆ asking you"));
            Assert.That(Row("research").ScreenName, Is.EqualTo("research, asking you"));
            Assert.That(Row("old-child").StateText, Is.EqualTo("✓ done"));
            Assert.That(Row("lead").StateText, Is.EqualTo("■ stopped"));
        });
    }

    [Test]
    public void AStartAskedOfTheOwner_ShowsOnItsRow()
    {
        _chains.StartAsked = key => key == "build";

        _chains.Update();

        Assert.That(Row("build").StateText, Is.EqualTo("⏵ start asked"));
    }

    [Test]
    public void ARowsState_FollowsItsSession_WithoutRebuildingTheTree()
    {
        _chains.Update();
        var before = Row("build");

        Find("build").Lifecycle = SessionLifecycle.Idle;
        _chains.Update();

        Assert.That(Row("build"), Is.SameAs(before));
        Assert.That(before.StateText, Is.EqualTo("○ idle"));
    }

    [Test]
    public void TheFilters_FollowACardsState_WithoutARebuild()
    {
        _chains.Update();
        _chains.ActiveOnly = true;
        Assume.That(Row("build").IsShown, Is.False);

        Find("build").Lifecycle = SessionLifecycle.Running;
        _chains.Update();

        Assert.That(Row("build").IsShown, Is.True, "a card that starts running shows under Active only at once");
        Assert.That(Row("lead").IsContext, Is.True);
    }

    [Test]
    public void TheSearch_ReadsACardsCurrentFields_WithoutARebuild()
    {
        _chains.Update();
        var card = Find("build");
        card.Workstream = card.Workstream with { JiraKeys = ["ABC-42"] };
        _chains.Update();

        _chains.SearchText = "abc-42";

        Assert.That(Row("build").IsShown, Is.True);
        Assert.That(Row("build").IsContext, Is.False);
    }

    [Test]
    public void TheFilterButton_TellsAScreenReaderHowManyFiltersAreOn()
    {
        _chains.Update();

        Assert.That(_chains.FilterHelpText, Is.EqualTo("No filters on"));

        _chains.ActiveOnly = true;
        _chains.HideCompleted = true;

        Assert.That(_chains.FilterHelpText, Is.EqualTo("2 filters on"));
    }

    [Test]
    public void ASearch_ShowsTheMatch_AndItsAncestorsAsContext()
    {
        _chains.Update();

        _chains.SearchText = "build";

        Assert.Multiple(() =>
        {
            Assert.That(Row("build").IsShown, Is.True);
            Assert.That(Row("build").IsContext, Is.False);
            Assert.That(Row("build").TitleHighlights, Is.Not.Empty);
            Assert.That(Row("research").IsContext, Is.True);
            Assert.That(Row("lead").IsContext, Is.True);
            Assert.That(Row("old-lead").IsShown, Is.False);
            Assert.That(_chains.NoMatchText, Is.Empty);
        });
    }

    [Test]
    public void ASearchMatchingNothing_SaysSo()
    {
        _chains.Update();

        _chains.SearchText = "zzz";

        Assert.That(_chains.NoMatchText, Is.EqualTo("No chain card matches 'zzz'"));
    }

    [Test]
    public void ASearch_UnfoldsWhatItNeeds_AndPutsTheFoldsBackWhenCleared()
    {
        _chains.Update();
        Row("lead").IsExpanded = false;

        _chains.SearchText = "build";
        Assert.That(Row("lead").IsExpanded, Is.True, "unfolded to show the match");

        _chains.SearchText = string.Empty;
        Assert.That(Row("lead").IsExpanded, Is.False, "folded again as it was");
        Assert.That(_settings.Settings.ChainsFolds["lead"], Is.False, "the search never changed the remembered fold");
    }

    [Test]
    public void TheSearch_AlsoNarrowsReady()
    {
        _cards.Add(Card("fresh"));
        var link = _book.Add(LinkKind.HandsOver, "old-child", "fresh", output: @"C:\work\REPORT.md").Link!;
        _book.Satisfy(link.Id, "read it");
        _chains.Update();
        Assume.That(_chains.ShownReady.Select(node => node.Key), Does.Contain("fresh"));

        _chains.SearchText = "zzz";

        Assert.That(_chains.ShownReady, Is.Empty);
    }

    [Test]
    public void HideCompleted_HidesTheFinishedChain_AndSaysHowManyItHides()
    {
        MarkDone("old-lead");
        MarkDone("old-child");
        _chains.Update();

        _chains.HideCompleted = true;

        Assert.Multiple(() =>
        {
            Assert.That(Row("old-lead").IsShown, Is.False);
            Assert.That(Row("old-child").IsShown, Is.False);
            Assert.That(Row("lead").IsShown, Is.True, "stopped is not completed");
            Assert.That(_chains.HideCompletedText, Is.EqualTo("Hide completed (2)"));
            Assert.That(_chains.HiddenSummary, Is.EqualTo("Showing 3 of 5 · 2 hidden by Hide completed"));
            Assert.That(_chains.FilterButtonText, Is.EqualTo("Filter · 1"));
        });
    }

    [Test]
    public void ActiveOnly_And_WaitingOnMe_Combine()
    {
        Find("build").Lifecycle = SessionLifecycle.Running;
        Find("research").Lifecycle = SessionLifecycle.NeedsAttention;
        _chains.Update();

        _chains.ActiveOnly = true;
        Assert.That(Row("build").IsShown, Is.True);

        _chains.WaitingOnMe = true;

        Assert.Multiple(() =>
        {
            Assert.That(Row("build").IsShown, Is.False, "running, but nothing waits on the owner");
            Assert.That(Row("research").IsShown, Is.True);
            Assert.That(Row("research").IsContext, Is.False);
            Assert.That(Row("lead").IsContext, Is.True);
            Assert.That(_chains.FilterButtonText, Is.EqualTo("Filter · 2"));
        });
    }

    [Test]
    public void EverythingHiddenByFilters_SaysSo_AndClearFiltersBringsItBack()
    {
        _chains.Update();

        _chains.WaitingOnMe = true;
        Assert.That(_chains.AllHiddenText, Is.EqualTo("Every chain is hidden by your filters"));

        _chains.ClearFiltersCommand.Execute(null);

        Assert.That(_chains.AllHiddenText, Is.Empty);
        Assert.That(Row("lead").IsShown, Is.True);
        Assert.That(_settings.Settings.ChainsFilters, Is.EqualTo(new ChainFilters(false, false, false)));
    }

    [Test]
    public void TheFilters_AreRemembered_BetweenRuns()
    {
        _chains.Update();
        _chains.ActiveOnly = true;

        var again = NewChains();

        Assert.That(again.ActiveOnly, Is.True);
        Assert.That(again.HideCompleted, Is.False);
    }

    [Test]
    public void AFinishedSubChain_StartsFolded_AndTheRestUnfolded()
    {
        MarkDone("old-child");

        _chains.Update();

        Assert.That(Row("old-lead").IsExpanded, Is.False, "everything below it is done");
        Assert.That(Row("lead").IsExpanded, Is.True);
    }

    [Test]
    public void WhatTheOwnerOpens_IsRemembered_ThroughARebuildAndARestart()
    {
        MarkDone("old-child");
        _chains.Update();

        Row("old-lead").IsExpanded = true;
        _book.Add(LinkKind.HandsOver, "lead", "old-lead");
        _chains.Update();
        Assert.That(Row("old-lead").IsExpanded, Is.True, "kept through a rebuild");

        var again = NewChains();
        again.Update();

        Assert.That(Rows(again).Single(row => row.Key == "old-lead").IsExpanded, Is.True, "kept through a restart");
    }

    [Test]
    public void FoldAll_IsRemembered_ForEveryRow()
    {
        _chains.Update();

        _chains.FoldAllCommand.Execute(null);
        var again = NewChains();
        again.Update();

        Assert.That(Rows(again).Where(row => row.Children.Count > 0).Select(row => row.IsExpanded), Is.All.False);
    }

    [Test]
    public void ChainsInOneProject_HaveNoHeaders()
    {
        InProject("lead", "theseus");
        InProject("old-lead", "theseus");

        _chains.Update();

        Assert.That(_chains.Roots.Any(row => row.IsGroup), Is.False);
    }

    [Test]
    public void ChainsInTwoProjects_AreGroupedUnderAHeaderEach_WithNoProjectLast()
    {
        _cards.Add(Card("solo"));
        _cards.Add(Card("solo-child"));
        _book.Add(LinkKind.HandsOver, "solo", "solo-child");
        InProject("lead", "theseus");
        InProject("old-lead", "bym");
        _chains.ProjectTitle = name => name == "bym" ? "BuildYourMarket" : "Theseus";

        _chains.Update();

        Assert.Multiple(() =>
        {
            Assert.That(_chains.Roots.Select(row => row.Title),
                Is.EqualTo(new[] { "BuildYourMarket · 2", "Theseus · 3", "Not in a project · 2" }));
            Assert.That(_chains.Roots.Single(row => row.Title.StartsWith("Theseus")).Children.Single().Key, Is.EqualTo("lead"));
            Assert.That(_chains.Roots.First().ScreenName, Is.EqualTo("BuildYourMarket · 2"));
        });
    }

    [Test]
    public void AProjectsHeader_IsHiddenWhenNothingUnderItShows()
    {
        InProject("lead", "theseus");
        InProject("old-lead", "bym");
        _chains.Update();

        _chains.SearchText = "build";

        Assert.That(_chains.Roots.Single(row => row.Key == "project:bym").IsShown, Is.False);
        Assert.That(_chains.Roots.Single(row => row.Key == "project:theseus").IsShown, Is.True);
    }

    private ChainsViewModel NewChains() =>
        new(_book, () => _cards, () => BriefFolder.DefaultName, () => { }, _ => { }) { Settings = _settings };

    private void MarkDone(string key) => Find(key).StopReason = new WhyStopped(WhyStoppedKind.Done, "✓ Done");

    private void InProject(string key, string project)
    {
        var card = Find(key);
        card.Workstream = card.Workstream with { Project = project };
    }

    private WorkstreamCardViewModel Find(string key) => _cards.Single(card => card.Key == key);

    private ChainNodeViewModel Row(string key) => Rows(_chains).Single(row => row.Key == key);

    private static IEnumerable<ChainNodeViewModel> Rows(ChainsViewModel chains) => Flatten(chains.Roots);

    private static IEnumerable<ChainNodeViewModel> Flatten(IEnumerable<ChainNodeViewModel> nodes) =>
        nodes.SelectMany(node => new[] { node }.Concat(Flatten(node.Children)));

    private static WorkstreamCardViewModel Card(string key) =>
        new(new Workstream(key, key, "#208DD7", false, @"C:\work", [], "Carry on.", 0, "New", false, false,
            false, null, [], null, Nine));

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
