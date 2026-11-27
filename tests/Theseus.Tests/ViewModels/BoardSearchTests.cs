using Theseus.App.ViewModels;
using Theseus.Core.Domain;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The board's search (card-search-and-tags items 1 to 8 and 15 to 17): it filters as you type, dims what does not match
/// or hides it, shows matches in folded rows and columns, counts them, steps through them, finds project cards through
/// their workstreams, and Clear puts the board back exactly as it was.
/// </summary>
[TestFixture]
public class BoardSearchTests
{
    private static BoardFixture Board()
    {
        var fixture = new BoardFixture()
            .With("bym-w7", area: "Dev").Retitle("bym-w7", "BYM MCP W7 - GUI parity, search-backed reads")
            .With("bym-w6", area: "Dev").Retitle("bym-w6", "BYM MCP W6 - GUI table reads")
            .With("theseus-search", column: "In progress", area: "Dev").Retitle("theseus-search", "Theseus finding cards")
            .With("gdpr", area: "Research").Retitle("gdpr", "GDPR whitepaper");
        fixture.Board.Refresh();

        return fixture;
    }

    private static WorkstreamCardViewModel CardOf(BoardFixture fixture, string key) => fixture.Card(key)!;

    private static IReadOnlyList<string> Matched(BoardFixture fixture) =>
        [.. fixture.Board.Matches.OfType<WorkstreamCardViewModel>().Select(card => card.Key)];

    [Test]
    public void Typing_FiltersAtOnce_SeveralWordsAllMatching()
    {
        var fixture = Board();

        fixture.Board.Search.Text = "bym search";

        Assert.That(Matched(fixture), Is.EqualTo(new[] { "bym-w7" }), "the spec's example: 'bym search' finds W7 only");
    }

    [Test]
    public void NonMatchingCards_AreDimmed_NotRemoved()
    {
        var fixture = Board();

        fixture.Board.Search.Text = "bym";

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.GridCards, Has.Count.EqualTo(4), "the board keeps its shape");
            Assert.That(CardOf(fixture, "gdpr").IsSearchDimmed, Is.True);
            Assert.That(CardOf(fixture, "bym-w6").IsSearchDimmed, Is.False);
        });
    }

    [Test]
    public void AMatchingCard_HasTheMatchedTextHighlighted()
    {
        var fixture = Board();

        fixture.Board.Search.Text = "gdpr";

        Assert.That(CardOf(fixture, "gdpr").TitleHighlights, Is.EqualTo(new[] { new Theseus.Core.Finding.TextSpan(0, 4) }));
    }

    [Test]
    public void HideTheRest_HidesWhatDoesNotMatch_AndTurningItOffBringsItBack()
    {
        var fixture = Board();
        fixture.Board.Search.Text = "bym";

        fixture.Board.Search.HideRest = true;
        var hidden = fixture.Board.GridCards.Select(card => card.Key).Order().ToList();
        fixture.Board.Search.HideRest = false;

        Assert.Multiple(() =>
        {
            Assert.That(hidden, Is.EqualTo(new[] { "bym-w6", "bym-w7" }));
            Assert.That(fixture.Board.GridCards, Has.Count.EqualTo(4));
        });
    }

    [Test]
    public void TheKeyProjectAreaJiraKeysBranchAndRepos_AreAllSearched()
    {
        var fixture = Board().Sidecar("gdpr", sidecar => sidecar with { JiraKeys = ["DPO-17"] });
        fixture.Board.Refresh();
        var gdpr = CardOf(fixture, "gdpr");
        gdpr.Repos = [new CardRepo(@"C:\work\privacy-site", "privacy-site", "user/consent-banner")];

        string[] Finds(string typed)
        {
            fixture.Board.Search.Text = typed;

            return [.. Matched(fixture)];
        }

        Assert.Multiple(() =>
        {
            Assert.That(Finds("theseus-sea"), Is.EqualTo(new[] { "theseus-search" }), "the key");
            Assert.That(Finds("researc"), Is.EqualTo(new[] { "gdpr" }), "the area");
            Assert.That(Finds("dpo-1"), Is.EqualTo(new[] { "gdpr" }), "a Jira key");
            Assert.That(Finds("consent"), Is.EqualTo(new[] { "gdpr" }), "a branch");
            Assert.That(Finds("privacy"), Is.EqualTo(new[] { "gdpr" }), "a repo");
        });
    }

    [Test]
    public void TheProject_IsSearched()
    {
        var fixture = Board();
        var card = CardOf(fixture, "theseus-search");
        card.Workstream = card.Workstream with { Project = "Theseus" };

        fixture.Board.Search.Text = "theseus";
        var withProject = Matched(fixture);
        card.Workstream = card.Workstream with { Title = "Other", Key = "other-key", Project = "Theseus" };
        fixture.Board.Regroup();

        Assert.Multiple(() =>
        {
            Assert.That(withProject, Does.Contain("theseus-search"));
            Assert.That(Matched(fixture), Does.Contain("other-key"), "found by its project alone");
        });
    }

    [Test]
    public void AFoldedRowOrColumn_HoldingAMatch_SaysSo_AndDrawsIt_WithoutUnfolding()
    {
        var fixture = Board();
        var dev = fixture.Board.Rows.Single(row => row.Area == "Dev");
        var progress = fixture.Board.StageHeaders.Single(header => header.Stage == "In progress");
        fixture.Board.ToggleRow(dev);
        fixture.Board.ToggleStage(progress);

        fixture.Board.Search.Text = "bym";
        var devMatch = dev.MatchText;
        var strip = dev.Matched.Select(item => item.Title).ToList();
        fixture.Board.Search.Text = "finding";

        Assert.Multiple(() =>
        {
            Assert.That(devMatch, Is.EqualTo("2 match"));
            Assert.That(strip, Has.Count.EqualTo(2), "the matches are drawn in the row's strip");
            Assert.That(progress.MatchText, Is.EqualTo("1 match"));
            Assert.That(dev.IsCollapsed, Is.True, "nothing folded is unfolded");
            Assert.That(progress.IsCollapsed, Is.True);
        });
    }

    [Test]
    public void TheStatusBar_CountsTheMatches()
    {
        var fixture = Board();

        fixture.Board.Search.Text = "bym";

        Assert.That(fixture.Board.Search.StatusText, Is.EqualTo("2 of 4 cards match"));
    }

    [Test]
    public void NoMatch_SaysSo_AndOffersIncludeArchived_OnlyWhenAnArchivedCardWouldMatch()
    {
        var fixture = Board().With("old-audit").Retitle("old-audit", "Old audit").Archive("old-audit");
        fixture.Board.Refresh();

        fixture.Board.Search.Text = "audit";
        var offered = fixture.Board.Search.ShowsIncludeArchived;
        var said = fixture.Board.Search.StatusText;
        fixture.Board.Search.Text = "xyz";

        Assert.Multiple(() =>
        {
            Assert.That(said, Is.EqualTo("No card matches 'audit'"));
            Assert.That(offered, Is.True);
            Assert.That(fixture.Board.Search.ShowsIncludeArchived, Is.False, "nothing archived matches xyz");
        });
    }

    [Test]
    public void IncludeArchived_ShowsTheArchive_KeepingTheSearch()
    {
        var fixture = Board().With("old-audit").Retitle("old-audit", "Old audit").Archive("old-audit");
        fixture.Board.Refresh();
        fixture.Board.Search.Text = "audit";

        fixture.Board.IncludeArchived();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.ShowArchived, Is.True);
            Assert.That(Matched(fixture), Is.EqualTo(new[] { "old-audit" }));
        });
    }

    [Test]
    public void Enter_SelectsTheFirstMatch_F3TheNext_ShiftF3TheOneBefore()
    {
        var fixture = Board();
        fixture.Board.Search.Text = "bym";
        var order = Matched(fixture);

        var first = fixture.Board.StepMatch(forward: true);
        var second = fixture.Board.StepMatch(forward: true);
        var back = fixture.Board.StepMatch(forward: false);

        Assert.Multiple(() =>
        {
            Assert.That(((WorkstreamCardViewModel)first!).Key, Is.EqualTo(order[0]));
            Assert.That(((WorkstreamCardViewModel)second!).Key, Is.EqualTo(order[1]));
            Assert.That(back, Is.SameAs(first));
            Assert.That(fixture.Board.ShownItems.Where(item => item.IsSelected), Is.EqualTo(new[] { first }),
                "only the match moved to is selected");
            Assert.That(fixture.Board.CurrentMatch, Is.SameAs(first), "what Ctrl+Enter opens");
        });
    }

    [Test]
    public void TheKeys_DoNothing_WithNoSearch()
    {
        var fixture = Board();

        Assert.That(fixture.Board.StepMatch(forward: true), Is.Null);
    }

    [Test]
    public void AProjectCard_MatchesThroughItsWorkstreams_AndSaysHowManyInside()
    {
        var fixture = Board();
        fixture.Board.KeepOutOfGrid = card => card.Key is "bym-w7" or "bym-w6";
        var project = new ProjectCardViewModel("bym-mcp", "BuildYourMarket MCP") { MemberKeys = ["bym-w7", "bym-w6"] };
        fixture.Board.ShowProjects([project]);

        fixture.Board.Search.Text = "w7";

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.Matches, Does.Contain(project));
            Assert.That(project.MatchesInsideText, Is.EqualTo("1 in BuildYourMarket MCP"));
            Assert.That(project.MatchingKeys, Is.EqualTo(new[] { "bym-w7" }), "what opening it carries into the project view");
            Assert.That(project.IsSearchDimmed, Is.False);
        });
    }

    [Test]
    public void AProjectCard_MatchesThroughAVerifierOfItsCards()
    {
        // "Verify: Theseus" found 0 of 69 (Nigel, 2026-11-23): a verifier is kept off the grid with its project's cards, but
        // is not among the cards the project's pieces name, so neither it nor its project was found.
        var fixture = Board().With("verify-w7", area: "Dev").Retitle("verify-w7", "Verify: BYM MCP W7");
        fixture.Board.Refresh();
        var verifier = CardOf(fixture, "verify-w7");
        verifier.Workstream = verifier.Workstream with { Project = "bym-mcp" };
        fixture.Board.KeepOutOfGrid = card => card.Key is "bym-w7" or "bym-w6" or "verify-w7";
        var project = new ProjectCardViewModel("bym-mcp", "BuildYourMarket MCP") { MemberKeys = ["bym-w7", "bym-w6"] };
        fixture.Board.ShowProjects([project]);

        fixture.Board.Search.Text = "Verify: BYM";

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.Matches, Does.Contain(project));
            Assert.That(project.MatchingKeys, Is.EqualTo(new[] { "verify-w7" }));
            Assert.That(project.MatchesInsideText, Is.EqualTo("1 in BuildYourMarket MCP"));
            Assert.That(fixture.Board.Search.StatusText, Does.StartWith("1 of"));
        });
    }

    [Test]
    public void AProjectCard_MatchesThroughItsScenarioWriter()
    {
        // "nothing shows when I search for write holdout" (Nigel, 2026-11-26): no project file names a writer, so it carried
        // no project and its project card was never found through it.
        var fixture = Board().With("writer", area: "Dev").Retitle("writer", "Write scenarios for BYM MCP");
        fixture.Board.Refresh();
        CardOf(fixture, "writer").ProjectOfRole = "bym-mcp";
        fixture.Board.KeepOutOfGrid = card => card.Key is "bym-w7" or "bym-w6";
        var project = new ProjectCardViewModel("bym-mcp", "BuildYourMarket MCP") { MemberKeys = ["bym-w7", "bym-w6"] };
        fixture.Board.ShowProjects([project]);

        fixture.Board.Search.Text = "write scenarios";

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.Matches, Does.Contain(project));
            Assert.That(project.MatchingKeys, Is.EqualTo(new[] { "writer" }));
            Assert.That(Matched(fixture), Does.Contain("writer"), "the writer's own card, in the grid, is found too");
        });
    }

    [Test]
    public void AProjectCard_MatchesOnItsOwnTitle()
    {
        var fixture = Board();
        var project = new ProjectCardViewModel("bym-mcp", "BuildYourMarket MCP");
        fixture.Board.ShowProjects([project]);

        fixture.Board.Search.Text = "buildyour";

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.Matches, Does.Contain(project));
            Assert.That(project.MatchesInside, Is.Zero);
            Assert.That(project.TitleHighlights, Is.Not.Empty);
        });
    }

    [Test]
    public void AProjectCardThatDoesNotMatch_IsDimmed()
    {
        var fixture = Board();
        var project = new ProjectCardViewModel("other", "Other project");
        fixture.Board.ShowProjects([project]);

        fixture.Board.Search.Text = "gdpr";

        Assert.Multiple(() =>
        {
            Assert.That(project.IsSearchDimmed, Is.True);
            Assert.That(project.CardOpacity, Is.LessThan(0.5));
        });
    }

    [Test]
    public void TheTitleLine_SaysTheBoardIsFiltered()
    {
        var fixture = Board();

        fixture.Board.Search.Text = "bym";

        Assert.That(fixture.Board.Search.FilteredLine, Is.EqualTo("Filtered: 'bym' · 2 of 4"));
    }

    [Test]
    public void Clear_RestoresTheBoardExactlyAsBefore()
    {
        var fixture = Board();
        var before = fixture.Board.GridCards.Select(card => card.Key).ToList();
        fixture.Board.Search.Text = "bym";
        fixture.Board.Search.HideRest = true;

        fixture.Board.Search.ClearCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.GridCards.Select(card => card.Key), Is.EqualTo(before));
            Assert.That(fixture.Board.Cards.Any(card => card.IsSearchDimmed || card.TitleHighlights.Count > 0), Is.False);
            Assert.That(fixture.Board.Search.IsOn, Is.False);
            Assert.That(fixture.Board.Search.FilteredLine, Is.Empty);
            Assert.That(fixture.Board.Search.StatusText, Is.Empty);
            Assert.That(fixture.Board.Rows.All(row => row.MatchCount == 0), Is.True);
        });
    }

    [Test]
    public void Clear_IsDisabledWithAReason_WhenNothingIsOn()
    {
        var fixture = Board();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.Search.ClearCommand.CanExecute(null), Is.False);
            Assert.That(fixture.Board.Search.ClearToolTip, Does.StartWith("Nothing to clear"));
        });
    }

    [Test]
    public void TheSearch_IsNotRemembered_ButItsHistoryIsKeptForTheRun()
    {
        var fixture = Board();
        fixture.Board.Search.Text = "bym";
        fixture.Board.Search.Remember();

        var reopened = fixture.Reopen();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.Search.History, Is.EqualTo(new[] { "bym" }));
            Assert.That(reopened.Search.Text, Is.Empty, "a new run starts with no search");
            Assert.That(reopened.Search.History, Is.Empty);
        });
    }

    [Test]
    public void WhoeverHearsTheFilterChange_ReadsTheNewMatches()
    {
        var fixture = Board();
        var heard = new List<bool>();
        fixture.Board.Search.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BoardSearchViewModel.Filter))
            {
                heard.Add(fixture.Board.IsMatch(CardOf(fixture, "gdpr")));
            }
        };

        fixture.Board.Search.Text = "bym";
        fixture.Board.Search.Text = "gdpr";

        Assert.That(heard, Is.EqualTo(new[] { false, true }), "the project view's highlight is never a keystroke behind (review R1)");
    }

    [Test]
    public void RememberingASearch_MovesItsEntry_NeverEmptyingTheList()
    {
        var fixture = Board();
        var search = fixture.Board.Search;
        search.Text = "one";
        search.Remember();
        search.Text = "two";
        search.Remember();
        var emptied = false;
        search.History.CollectionChanged += (_, e) => emptied |= e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset;

        search.Text = "one";
        search.Remember();

        Assert.Multiple(() =>
        {
            Assert.That(search.History, Is.EqualTo(new[] { "one", "two" }));
            Assert.That(emptied, Is.False, "clearing the list the box shows would empty the search (review R2)");
            Assert.That(search.Text, Is.EqualTo("one"));
        });
    }

    [Test]
    public void CtrlEnter_OpensNothing_OnceTheOwnerHasSelectedAnotherCard()
    {
        var fixture = Board();
        fixture.Board.Search.Text = "bym";
        fixture.Board.StepMatch(forward: true);

        fixture.Board.ClickSelect(CardOf(fixture, "gdpr"), toggle: false, extend: false);

        Assert.That(fixture.Board.CurrentMatch, Is.Null, "a stale match is never opened (review R3)");
    }

    [Test]
    public void CtrlEnter_OpensTheSameCard_AfterARefreshReordersTheMatches()
    {
        var fixture = Board();
        fixture.Board.Search.Text = "bym";
        var first = fixture.Board.StepMatch(forward: true);

        fixture.Board.Refresh();

        Assert.That(fixture.Board.CurrentMatch, Is.SameAs(first));
    }

    [Test]
    public void Clear_TurnsHideTheRestOffToo_AndIsOfferedWhileItIsOn()
    {
        var fixture = Board();
        fixture.Board.Search.HideRest = true;
        var offered = fixture.Board.Search.ClearCommand.CanExecute(null);

        fixture.Board.Search.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(offered, Is.True);
            Assert.That(fixture.Board.Search.HideRest, Is.False, "the board shows exactly as before (item 16)");
        });
    }

    [Test]
    public void ARefresh_KeepsTheSearchApplied()
    {
        var fixture = Board();
        fixture.Board.Search.Text = "gdpr";

        fixture.Board.Refresh();

        Assert.That(CardOf(fixture, "bym-w7").IsSearchDimmed, Is.True);
    }
}
