using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The project view's model (1.1d AC 17 to 22, 26 to 30, 32 and 36), on the accepted mockup's two projects: the summary
/// and its counts, the Waiting on you band, the toolbar and legend, the tabs, each box's stages and actions, selection,
/// and the signature that decides a rebuild.
/// </summary>
[TestFixture]
public class ProjectViewModelTests
{
    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private static PieceNodeViewModel Box(ProjectViewModel view, string piece) => view.AllNodes.Single(node => node.Name == piece);

    private static StageNodeViewModel StageOf(ProjectViewModel view, string piece, string stage) =>
        Box(view, piece).Stages.Single(each => each.Name == stage);

    [Test]
    public void TheSummary_IsTheModelsSentence_AndSixChipsZerosIncluded()
    {
        var view = _views.Theseus();

        Assert.That(view.Sentence, Is.EqualTo(view.Overview.Sentence));
        Assert.That(view.Sentence, Is.EqualTo("Nothing is moving. Three things wait for you."), "1.1f G33, replacing 1.1d AC 8");
        Assert.That(view.Counts.Select(chip => chip.Text), Is.EqualTo(new[]
        {
            "✓ 7 done", "● 0 busy", "◆ 3 waiting on you", "◐ 0 not yet checked", "⚠ 0 stuck", "– 2 to do",
        }));
        Assert.That(view.Counts.Select(chip => chip.Count), Is.EqualTo(view.Counts.Select(chip => view.Summary.CountOf(chip.State))),
            "the counts are the model's, not worked out again (AC 10)");
    }

    [Test]
    public void WaitsForYou_HasALinePerWaitingPiece_InFileOrder_EachWithItsButton()
    {
        var view = _views.Theseus();

        Assert.That(view.HasWaits, Is.True);
        Assert.That(view.Waits.Select(wait => (wait.Item.Pieces.Single(), wait.Button)), Is.EqualTo(new[]
        {
            ("project-view", "Accept spec…"), ("project-home-folders", "Accept spec…"), ("projects-and-method-ui", "Accept spec…"),
        }));
        Assert.That(_views.BuildYourMarket().Waits.Select(wait => wait.Item.Kind), Has.None.EqualTo(WaitKind.AcceptSpec));
    }

    [Test]
    public void AWaitsButton_AcceptsThatPiecesSpec_AsItsBoxsButtonDoes()
    {
        var view = _views.Theseus();

        view.Waits[0].Command!.Execute(null);

        Assert.That(_views.Ran, Is.EqualTo(new[] { "AcceptSpec:project-view" }));
    }

    [Test]
    public void ASessionAsking_HasAnAnswerItButton_ThatFocusesItsTerminal()
    {
        var view = _views.One("feature", Files("research.md"), SessionLifecycle.NeedsAttention);
        var wait = view.Waits.Single();

        Assert.That((wait.Button, wait.Text), Is.EqualTo(("Answer it", "Something: You; it does nothing until you answer.")));

        wait.Command!.Execute(null);

        Assert.That(_views.Ran, Is.EqualTo(new[] { "Focus:a-card" }));
    }

    [Test]
    public void AProposalsWaitsButton_OpensTheProposalWindow_ForThatProposal()
    {
        var proposal = new Theseus.Core.Proposals.WorkstreamProposal("pr-7", Theseus.Core.Proposals.ProposalStatus.Pending, ACard, "s",
            new Theseus.Core.Proposals.ProposalRequest("A follow-up", "brief.md", false, null, "done", [], null), [],
            DateTimeOffset.UnixEpoch);
        var wait = _views.OneWith([proposal]).Waits.Single(each => each.Item.Kind == WaitKind.Proposal);

        Assert.That(wait.Button, Is.EqualTo("Review…"));

        wait.Command!.Execute(null);

        Assert.That(_views.Ran, Is.EqualTo(new[] { "ReviewProposal:pr-7" }), "sessions-run-the-project 0.2: not the card's links");
    }

    [Test]
    public void ASessionsStartOfACardYouPaused_WaitsForYou_AndItsButtonAsks()
    {
        var waiting = new StartRequest("sr-1", ACard, "lead", null, "its spec is accepted", DateTimeOffset.UnixEpoch);
        var elsewhere = new StartRequest("sr-2", "not-in-this-project", "lead", null, null, DateTimeOffset.UnixEpoch);
        var decided = new StartRequest("sr-3", ACard, "lead", null, null, DateTimeOffset.UnixEpoch, StartRequestStatus.Refused);
        var wait = _views.OneWith([], [waiting, elsewhere, decided]).Waits.Single(each => each.Item.Kind == WaitKind.StartRequest);

        Assert.That((wait.Text, wait.Button), Is.EqualTo(
            ("title of lead wants to start Something, which you paused: its spec is accepted", "Allow or refuse…")));

        wait.Command!.Execute(null);

        Assert.That(_views.Ran, Is.EqualTo(new[] { "AnswerStartRequest:sr-1" }));
    }

    [Test]
    public void ABugsWaitsButton_AcceptsTheRule()
    {
        var view = _views.One("bug", new Dictionary<string, string>
        {
            ["reproduction.md"] = "# Reproduction\n", ["fix.md"] = "# Fix\n\n## Spec\nThe rule.\n",
        });

        view.Waits.Single().Command!.Execute(null);

        Assert.That(_views.Ran, Is.EqualTo(new[] { "AcceptSpec:a-piece" }));
    }

    [Test]
    public void TheChipFilter_ShowsOneState_CtrlAddsOne_TheSameChipClears_AndARebuildKeepsIt()
    {
        var view = _views.BuildYourMarket();

        view.ToggleFilter(StageNodeState.HoldoutsNotRun, add: false);

        Assert.That(view.AllNodes.Where(node => node.IsShown).Select(node => node.State).Distinct(),
            Is.EquivalentTo(new[] { StageNodeState.HoldoutsNotRun }), "a piece sits under its own state's heading, not its parent (git-22, 2026-11-24)");
        Assert.That(view.FilterText, Does.StartWith("Showing: built, not yet checked (5 of"));

        view.ToggleFilter(StageNodeState.NotInProject, add: true);

        Assert.That(view.Filter, Is.EquivalentTo(new[] { StageNodeState.HoldoutsNotRun, StageNodeState.NotInProject }));

        var rebuilt = _views.BuildYourMarket(view);

        Assert.That(rebuilt.Filter, Is.EquivalentTo(view.Filter), "a refresh keeps it on");

        rebuilt.ToggleFilter(StageNodeState.HoldoutsNotRun, add: true);
        rebuilt.ToggleFilter(StageNodeState.NotInProject, add: false);

        Assert.That((rebuilt.IsFiltered, rebuilt.AllNodes.All(node => node.IsShown)), Is.EqualTo((false, true)));
    }

    [Test]
    public void Esc_ClearsTheFilter_WhenNothingIsSelected()
    {
        var view = _views.BuildYourMarket();
        view.ToggleFilter(StageNodeState.Done, add: false);

        view.ClearSelectionCommand.Execute(null);

        Assert.That(view.IsFiltered, Is.False);
    }

    [Test]
    public void CollapseAllAndExpandAll_FoldAndOpenEveryBox()
    {
        var view = _views.Theseus();

        view.ExpandAllCommand.Execute(null);
        Assert.That(view.AllNodes, Has.All.Matches<PieceNodeViewModel>(node => node.IsOpen));

        view.CollapseAllCommand.Execute(null);
        Assert.That(view.AllNodes, Has.None.Matches<PieceNodeViewModel>(node => node.IsOpen));
    }

    [Test]
    public void OpenWhatNeedsAttention_OpensBusyWaitingStuckAndHoldouts_AndFoldsTheRest()
    {
        var view = _views.Theseus(SessionLifecycle.Running);
        view.ExpandAllCommand.Execute(null);

        view.OpenWhatNeedsAttentionCommand.Execute(null);

        Assert.That(view.AllNodes.Where(node => node.IsOpen).Select(node => node.Name),
            Is.EqualTo(new[] { "project-view", "project-home-folders", "projects-and-method-ui" }));
        Assert.That(view.AllNodes.Where(node => node.IsOpen).Select(node => node.State),
            Has.All.AnyOf(StageNodeState.Busy, StageNodeState.WaitingOnYou, StageNodeState.Stuck, StageNodeState.HoldoutsNotRun));
    }

    [Test]
    public void TheToolbar_ActsOnTheOpenProjectOnly()
    {
        var theseus = _views.Theseus();
        var buildYourMarket = _views.BuildYourMarket();

        theseus.ExpandAllCommand.Execute(null);

        Assert.That(buildYourMarket.AllNodes, Has.None.Matches<PieceNodeViewModel>(node => node.IsOpen));
        Assert.That(_views.Settings.Settings.ViewFolds.Keys, Is.EquivalentTo(new[] { PieceFolds.KeyFor(TheseusProject) }));
    }

    [Test]
    public void TheLegend_NamesEachStateWithItsMark_InTheSpecsOrder()
    {
        Assert.That(ProjectViewModel.Legend.Select(item => $"{item.Mark} {item.Word}"), Is.EqualTo(new[]
        {
            "✓ Done", "◆ Waiting for you", "● Busy", "⚠ Stuck", "◐ Built, not yet checked", "– Not started",
        }));
    }

    [Test]
    public void TheTabs_AreOnePerProjectThatReads_TheOpenOneMarked()
    {
        WriteBuildYourMarket(_folder);
        _folder.Write("broken/project.md", "# Not a project file in any shape\n");

        var view = _views.Theseus();

        Assert.That(view.Tabs.Select(tab => (tab.Title, tab.IsOpen)), Is.EquivalentTo(new[]
        {
            ("Theseus bootstrap", true), ("BuildYourMarket MCP server", false),
        }));
    }

    [Test]
    public void AProjectTab_OpensItsProject()
    {
        var opened = new List<string>();
        WriteTheseus(_folder);

        var tabs = ProjectTabViewModel.For(ProjectCatalog.Read(_folder.Folder), "elsewhere",
            name => new RelayCommand(_ => opened.Add(name)));
        tabs.Single().OpenCommand!.Execute(null);

        Assert.That(opened, Is.EqualTo(new[] { TheseusProject }));
        Assert.That(tabs.Single().IsOpen, Is.False);
    }

    [Test]
    public void AFoldedBox_ShowsItsNameMetaStatusAndAStripOfStages()
    {
        var box = Box(_views.Theseus(), "readme-runtime");

        Assert.That((box.IsOpen, box.BoxWidth, box.Chevron, box.ChevronToolTip), Is.EqualTo((false, PieceNodeViewModel.FoldedWidth, FoldGlyph.Folded, "Open into its stages")));
        Assert.That((box.Workstream, box.KindWord, box.ObjectiveLabel), Is.EqualTo(("git-22", "bug", "objective 0.3")));
        Assert.That(box.ColourHex, Is.EqualTo("#2f80ed"));
        Assert.That(box.StatusText, Is.EqualTo("– Next: Review."));
        Assert.That(box.Stages.Select(stage => stage.Abbreviation), Is.EqualTo(new[] { "Rep", "Rul", "Fix", "Ver", "Rev", "Com" }));
        Assert.That(box.Stages[0].ToolTip, Is.EqualTo("Reproduce: Done"));
    }

    [Test]
    public void AReportBack_IsAMarkOnTheChildsBox_WithItsTooltip()
    {
        var view = _views.BuildYourMarket();
        var identity = Box(view, "bym-mcp-identity");
        var fixes = Box(view, ClientFixes);

        Assert.That((identity.ReportBackText, identity.ReportBackToolTip), Is.EqualTo(
            ("↩ reported back to Specify the BuildYourMarket MCP server", "Reported back to Specify the BuildYourMarket MCP server")));
        Assert.That(fixes.ReportBackText, Is.EqualTo("↩ reports back to Specify the BuildYourMarket MCP server"));
        Assert.That(Box(view, ServerSpec).HasReportBack, Is.False);
    }

    [Test]
    public void APieceWhoseCardIsNotOnTheBoard_IsGrey()
    {
        var view = _views.Of(WriteTheseus(_folder), []);

        Assert.That(view.AllNodes.Select(node => node.ColourHex).Distinct(), Is.EqualTo(new[] { PieceNodeViewModel.NoCardColour }));
    }

    [Test]
    public void AnOpenBox_Is640Wide_WithItsActionsInOrder()
    {
        var view = _views.Theseus();
        var box = Box(view, "projects-and-method-ui");

        view.Toggle(box);

        Assert.That((box.IsOpen, box.BoxWidth, box.Chevron, box.ChevronToolTip), Is.EqualTo((true, 640.0, FoldGlyph.Open, "Fold this piece of work")));
        Assert.That(box.Actions.Select(action => action.Label), Is.EqualTo(new[]
        {
            "Open spec", "Accept spec…", "Independent check…", "⧉ Focus git-22", "■ Stop", "Open records",
        }));
        Assert.That(box.Actions.Single(action => action.Label.StartsWith('⧉')).ToolTip, Is.EqualTo("Bring its terminal forward"));
    }

    [Test]
    public void ABoxWhoseSessionRuns_ShowsItsMarkAndFocus_OnItsTitleBar()
    {
        var running = Box(_views.Theseus(SessionLifecycle.Running), "projects-and-method-ui");
        var stopped = Box(_views.Theseus(SessionLifecycle.NotStarted), "projects-and-method-ui");
        running.FocusAction!.Command.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(running.SessionMark, Is.EqualTo("● running"));
            Assert.That(_views.Ran, Is.EqualTo(new[] { "Focus:git-22" }), "one click to its tab, folded or open");
            Assert.That(stopped.HasSessionMark, Is.False);
            Assert.That(stopped.FocusAction, Is.Null);
        });
    }

    [Test]
    public void ACardWithNoSession_OffersStart_NotFocusOrStop()
    {
        var box = Box(_views.Theseus(SessionLifecycle.NotStarted), "projects-and-method-ui");

        Assert.That(box.Actions.Select(action => action.Label), Is.EqualTo(new[]
        {
            "Open spec", "Accept spec…", "Independent check…", "▶ Start", "Open records",
        }));
    }

    [Test]
    public void InARunningProject_TheBoxSaysWhoStartsIt_AndOffersStartAnywaySecond()
    {
        // Nigel, 2026-11-23: a plain ▶ Start then asked "part of <project>, which decides what runs. Start it anyway?".
        const string who = "Theseus starts it, when the project's plan reaches it: Theseus is running and decides what runs.";
        var view = _views.One("freeform", actions: _views.Actions with { WhoStarts = _ => new ProjectRunStart(who) });
        var box = view.AllNodes.Single();
        var start = box.Actions.Single(action => action.Label == "Start anyway…");

        start.Command.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(box.Actions.Select(action => action.Label), Has.None.EqualTo("▶ Start"));
            Assert.That(box.WhoActs, Is.EqualTo(who));
            Assert.That(box.PrimaryAction, Is.Null, "folded, the box says who starts it and offers no button: the override is never its one button");
            Assert.That(start.ToolTip, Does.StartWith(who));
            Assert.That(_views.Ran, Is.EqualTo(new[] { $"Start:{box.Piece.CardKey}" }), "the same Start, which asks the same question first");
        });
    }

    [Test]
    public void InARunningProject_ABoxWaitingOnYouKeepsItsOwnAnswer()
    {
        // Found on the live data: a build waiting for its check read "its turn has not come yet".
        var view = _views.Theseus(SessionLifecycle.NotStarted, actions: _views.Actions with { WhoStarts = _ => new ProjectRunStart("Lead starts it.") });
        var box = Box(view, "projects-and-method-ui");

        Assert.That(box.Actions.Select(action => action.Label), Does.Contain("Start anyway…"));
        Assert.That(box.WhoActs, Is.EqualTo(box.Piece.Status.WhoActs));
    }

    [Test]
    public void InARunningProject_ACardTheOwnerHolds_StillHasStartAnyway_AndKeepsItsOwnAnswer()
    {
        var box = _views.One("freeform", actions: _views.Actions with { WhoStarts = _ => new ProjectRunStart(null) }).AllNodes.Single();

        Assert.That(box.Actions.Select(action => action.Label), Does.Contain("Start anyway…"));
        Assert.That(box.WhoActs, Is.EqualTo(box.Piece.Status.WhoActs));
    }

    [Test]
    public void WhereTheOwnerStartsIt_TheBoxKeepsAPlainStart()
    {
        var box = Box(_views.Theseus(SessionLifecycle.NotStarted, actions: _views.Actions with { WhoStarts = _ => null }), "projects-and-method-ui");

        Assert.That(box.Actions.Select(action => action.Label), Does.Contain("▶ Start"));
        Assert.That(box.StartedBy, Is.Null);
    }

    [Test]
    public void TheActions_RunTheShellsCommands_ForTheirCard()
    {
        var view = _views.Theseus(SessionLifecycle.NotStarted);
        var box = Box(view, "projects-and-method-ui");

        foreach (var action in box.Actions)
        {
            action.Command.Execute(null);
        }

        Assert.That(_views.Ran, Is.EqualTo(new[]
        {
            "OpenSpec:projects-and-method-ui", "AcceptSpec:projects-and-method-ui", "Verify:git-22", "Start:git-22",
            $"Open:{_folder.Home(TheseusProject)}\\work\\projects-and-method-ui",
        }));
    }

    [Test]
    public void OpenRecords_WithNoRecordsFolder_IsDisabledSayingSo()
    {
        var records = Box(_views.BuildYourMarket(), ClientFixes).Actions.Single(action => action.Label == "Open records");

        Assert.That((records.IsEnabled, records.ToolTip), Is.EqualTo((false, "No records folder in the project yet")));
        Assert.That(records.Command.CanExecute(null), Is.False);
    }

    [Test]
    public void VerifyWithHoldouts_OnAPieceItsCardHasLeft_IsDisabledSayingWhy()
    {
        var verify = Box(_views.Theseus(), "project-view").Actions.Single(action => action.Label == "Independent check…");

        Assert.That(verify.IsEnabled, Is.False);
        Assert.That(verify.ToolTip, Is.EqualTo(
            "git-22 has moved on to projects-and-method-ui; an independent check looks at a card's current work"));
    }

    [Test]
    public void AStagesMenu_HoldsPropertiesFirstAndBold_ThenItsActions()
    {
        var view = _views.Theseus();
        var spec = StageOf(view, "project-view", "Spec");

        Assert.That(spec.MenuItems.Select(item => item.Label), Is.EqualTo(new[]
        {
            "Properties", "Open spec", "Accept spec…", "Open records", "Open spec.md",
        }));
        Assert.That(spec.MenuItems.Select(item => item.IsDefault), Is.EqualTo(new[] { true, false, false, false, false }));

        spec.MenuItems[0].Command.Execute(null);

        Assert.That(view.Selection, Is.SameAs(spec));
    }

    [Test]
    public void StateIsNeverColourAlone_EveryStageChipAndStatusHasItsMarkAndWord()
    {
        var view = _views.Theseus(SessionLifecycle.Running);

        foreach (var node in view.AllNodes)
        {
            Assert.That(node.StatusText, Does.StartWith(StageNodeStates.Mark(node.State) + " "));

            foreach (var stage in node.Stages)
            {
                Assert.That(stage.Label, Is.EqualTo($"{stage.Mark} {stage.Name}"));
                Assert.That(stage.ToolTip, Is.EqualTo($"{stage.Name}: {StageNodeStates.Word(stage.State)}"));
            }
        }
    }

    [Test]
    public void SelectingAPiece_SelectsItsBox_AndAStage_SelectsTheStageAndItsBox()
    {
        var view = _views.Theseus();
        var box = Box(view, "stop-timing");

        box.SelectCommand.Execute(null);
        Assert.That((view.Selection, box.IsSelected), Is.EqualTo(((object)box, true)));

        box.Stages[2].SelectCommand.Execute(null);
        Assert.That(view.Selection, Is.SameAs(box.Stages[2]));
        Assert.That((box.Stages[2].IsSelected, box.IsSelected), Is.EqualTo((true, true)));

        Box(view, "card-branches").SelectCommand.Execute(null);
        Assert.That((box.Stages[2].IsSelected, box.IsSelected), Is.EqualTo((false, false)), "one thing is selected at a time");
    }

    [Test]
    public void Esc_ClearsTheSelection()
    {
        var view = _views.Theseus();
        Assert.That(view.ClearSelectionCommand.CanExecute(null), Is.False);

        Box(view, "stop-timing").SelectCommand.Execute(null);
        view.ClearSelectionCommand.Execute(null);

        Assert.That(view.Selection, Is.Null);
        Assert.That(view.Properties, Is.SameAs(PropertiesViewModel.Empty));
        Assert.That(Box(view, "stop-timing").IsSelected, Is.False);
    }

    [Test]
    public void AChevron_OnlyFoldsOrOpens_ItNeverSelects()
    {
        var view = _views.Theseus();

        Box(view, "stop-timing").ToggleCommand.Execute(null);

        Assert.That(view.Selection, Is.Null);
    }

    [Test]
    public void ARefresh_KeepsTheSelection_WhileThePieceAndStageStillExist()
    {
        var first = _views.Theseus();
        StageOf(first, "stop-timing", "Plan").SelectCommand.Execute(null);

        var second = _views.Theseus(SessionLifecycle.Running, previous: first);

        Assert.That(second.Selection, Is.SameAs(StageOf(second, "stop-timing", "Plan")));
        Assert.That(second.Properties.Heading, Is.EqualTo("Plan of Five changes through the method by hand"));
    }

    [Test]
    public void SwitchingProject_ClearsTheSelection()
    {
        var theseus = _views.Theseus();
        Box(theseus, "stop-timing").SelectCommand.Execute(null);

        var other = _views.BuildYourMarket(previous: theseus);

        Assert.That(other.Selection, Is.Null);
    }

    [Test]
    public void TheSameProjectBuiltTwice_HasTheSameSignature()
    {
        var first = _views.Theseus().Signature;
        var second = _views.Theseus().Signature;

        Assert.That(second, Is.EqualTo(first));
    }

    // 1.1 PR 2 review, item 4, kept for the rebuilt view: what is read but not drawn never rebuilds it. Records are now
    // plain files in the project's home (1.1b), with no branch or commit, so a rewrite and a fresh read are the cases.
    [Test]
    public void ARecordRewrittenWithoutChangingWhatIsDrawn_OrAFreshRead_KeepsTheSignature_SoTheViewIsNotRebuilt()
    {
        var before = _views.Theseus().Signature;

        _folder.Write($"{TheseusProject}/work/stop-timing/plan.md", "# A plan, rewritten in other words\n");
        _folder.Write($"{TheseusProject}/work/project-view/review.md", "# A review, rewritten\n");

        Assert.That(_views.Of(Read(_folder, TheseusProject), [Card(Git22, SessionLifecycle.Idle)]).Signature, Is.EqualTo(before),
            "records rewritten");
        Assert.That(_views.Of(Read(_folder, TheseusProject), [Card(Git22, SessionLifecycle.Idle)]).Signature, Is.EqualTo(before),
            "read again");
    }

    [Test]
    public void TheSignature_ChangesWithALiveSessionsState()
    {
        Assert.That(_views.Theseus(SessionLifecycle.Running).Signature, Is.Not.EqualTo(_views.Theseus(SessionLifecycle.Idle).Signature));
        Assert.That(_views.Theseus(SessionLifecycle.NotStarted).Signature, Is.Not.EqualTo(_views.Theseus(SessionLifecycle.Idle).Signature));
    }

    [Test]
    public void TheSignature_ChangesWithAStageState_AMarkAndAStart()
    {
        var before = _views.BuildYourMarket().Signature;
        _folder.Write($"{ProjectViewFixtures.BuildYourMarket}/work/bym-mcp-host/evidence.md", FailedEvidence);
        var reading = Read(_folder, ProjectViewFixtures.BuildYourMarket);
        var cards = BuildYourMarketBoard();
        var links = BuildYourMarketLinks();

        var stuck = _views.Of(reading, cards, links).Signature;
        var noMarks = _views.Of(reading, cards, [.. links.Where(link => link.Kind != Theseus.Core.Links.LinkKind.ReportsBack)]).Signature;

        Assert.That(stuck, Is.Not.EqualTo(before));
        Assert.That(noMarks, Is.Not.EqualTo(stuck));
        Assert.That(_views.Theseus(SessionLifecycle.NotStarted, canStart: false).Signature,
            Is.Not.EqualTo(_views.Theseus(SessionLifecycle.NotStarted).Signature), "Start offered or not");
    }

    [Test]
    public void TheSignature_ChangesWhenACriterionIsTicked()
    {
        var before = _views.Theseus().Signature;
        var path = Path.Combine(_folder.Home(TheseusProject), "project.md");
        File.WriteAllText(path, File.ReadAllText(path).Replace("- [ ] A criterion.", "- [x] A criterion.", StringComparison.Ordinal));

        var reading = Read(_folder, TheseusProject);
        var view = _views.Of(reading, [Card(Git22, SessionLifecycle.Idle)]);

        Assert.That(view.Signature, Is.Not.EqualTo(before));
    }
}
