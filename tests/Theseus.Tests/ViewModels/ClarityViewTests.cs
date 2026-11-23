using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ClarityFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// 1.1f's view on its two fixtures: rounds as the top level with the change banner, title bars, gate diamonds, fork and
/// join labels, folded runs of finished pieces, and the folded box's one action (A2, A3, C10 to C14, D18, D19, D21, D22,
/// H38).
/// </summary>
[TestFixture]
public class ClarityViewTests
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

    private ProjectViewModel Bym()
    {
        var reading = WriteBym(_folder);
        var pieces = BymPieces(_folder);

        return ProjectViewModel.Build(reading, pieces, ProjectViewFixtures.LinksOn(BymLinks(), BymBoard()), [], _views.Settings, _views.Actions);
    }

    private ProjectViewModel Theseus(IReadOnlyList<Link>? links = null)
    {
        var reading = WriteTheseus(_folder);
        var pieces = PiecesOfWork.Build(reading, slug => reading.Artefacts[slug].SpecHash is { } hash
            ? new SpecAcceptance(ClarityFixtures.Theseus, slug, hash, DateTimeOffset.UnixEpoch)
            : null, TheseusBoard(), links ?? []);

        return ProjectViewModel.Build(reading, pieces, ProjectViewFixtures.LinksOn(links ?? [], TheseusBoard()), [], _views.Settings,
            _views.Actions);
    }

    private static PieceNodeViewModel Box(ProjectViewModel view, string name) => view.AllNodes.Single(node => node.Name == name);

    [Test]
    public void BuildYourMarket_ListsItsTwoRounds_Round1Folded_AndRound2Open()
    {
        var view = Bym();

        Assert.That(view.Rounds.Select(round => (round.Title, round.Dates, round.IsOpen)), Is.EqualTo(new[]
        {
            ("Round 1: Specify, then build release 2a", "18 Nov – 21 Nov", false),
            ("Round 2: GUI parity", "21 Nov – now", true),
        }));
    }

    [Test]
    public void TheChangeBanner_SitsBeforeTheLaterRound_SayingWhenWhoAndWhy_AndNamingItsObjectives()
    {
        var rounds = Bym().Rounds;

        Assert.That(rounds[0].HasBanner, Is.False);
        Assert.That(rounds[1].Banner, Is.EqualTo(
            "⟲ The requirements changed, 21 Nov 2026, by Nigel. The tool catalogue was built from support tickets, not from what the GUI offers"));
        Assert.That(rounds[1].BannerObjectives, Is.EqualTo("Objectives added or reopened: 1.7, 1.8, 1.9, 1.10."));
    }

    [Test]
    public void ARound_SetsOutToMeetItsObjectives_EachWithItsCriteriaTicked()
    {
        var chips = Bym().Rounds[1].Objectives;

        Assert.That(chips.Select(chip => chip.Text), Is.EqualTo(new[]
        {
            "1.7 GUI parity, table-backed reads", "1.8 GUI parity, search-backed reads",
            "1.9 Search, table and blob read access (option A, D12)", "1.10 Specify the GUI-parity tools",
        }));
        Assert.That(chips[3].CriteriaText, Is.EqualTo("1 of 2 criteria met"));
        Assert.That(chips[3].FilledWidth, Is.EqualTo(30));
    }

    [Test]
    public void EachRound_HoldsItsOwnTree()
    {
        var rounds = Bym().Rounds;

        Assert.That(rounds[0].Items.OfType<PieceNodeViewModel>().Single().Title, Is.EqualTo("Specify the BuildYourMarket MCP server"));
        Assert.That(rounds[1].Items.OfType<PieceNodeViewModel>().Single().Children.Select(child => child.Name), Is.EqualTo(new[]
        {
            "bym-mcp-gui-table-reads", "bym-mcp-gui-search-reads", "bym-mcp-search-blob-access",
        }));
    }

    [Test]
    public void AFileWithNoRounds_ShowsRound1Open()
    {
        var round = Theseus().Rounds.Single();

        Assert.That((round.Title, round.IsOpen, round.HasBanner), Is.EqualTo(("Round 1", true, false)));
    }

    [Test]
    public void ARoundsFold_IsRemembered_AndCollapseAllLeavesRoundsAlone()
    {
        var view = Bym();
        view.Rounds[0].ToggleCommand.Execute(null);
        view.CollapseAllCommand.Execute(null);

        Assert.That(view.Rounds.Select(round => round.IsOpen), Is.EqualTo(new[] { true, true }));
        Assert.That(Bym().Rounds[0].IsOpen, Is.True, "Collapse all keeps round 1's own entry, so the next open matches the screen");
    }

    [Test]
    public void ABox_HasItsKindsTitleBar_WithTheKindsMeaningInItsHover()
    {
        var spec = Box(Bym(), Spec1);

        Assert.That((spec.KindLabel, spec.KindBrushKey, spec.Title), Is.EqualTo(("✎ Spec", "KindSpecBrush", "Specify the BuildYourMarket MCP server")));
        Assert.That(spec.KindToolTip, Does.Contain("hands the building on").And.EndWith("Worked out by Theseus from the pipeline and branch."));
    }

    [Test]
    public void TheForkAndJoin_SayHowManyRunInParallel_InWhatStates_AndHowManyHaveReportedBack()
    {
        var spec2 = Box(Bym(), Spec2);

        Assert.That(spec2.IsParallel, Is.True);
        Assert.That(spec2.ForkText, Does.StartWith("3 in parallel · "));
        Assert.That(spec2.ForkText, Does.Contain("1 answered, criteria not ticked"));
        Assert.That(spec2.JoinText, Is.EqualTo("Waiting for 2 of 3 to report back, then the Specify the GUI-parity tools session takes their reports"));
    }

    [Test]
    public void ASingleHandOver_HasNoBars()
    {
        var w1 = Box(Bym(), "bym-mcp-identity");

        Assert.That((w1.IsSingleHandOver, w1.IsParallel), Is.EqualTo((true, false)));
    }

    [Test]
    public void TheGateDiamond_SitsAfterSpec_PassedWaitingOrNotReached_AndFreeformHasNone()
    {
        var view = Bym();
        var w1 = Box(view, "bym-mcp-identity");
        var w7 = Box(view, "bym-mcp-gui-search-reads");

        Assert.That(w1.StripItems[2], Is.InstanceOf<GateMarkViewModel>(), "between Spec and Plan");
        Assert.That((w1.Gate!.Mark, w1.Gate.ToolTip), Is.EqualTo(("◆", "Passed: you accepted the spec " + DateTimeOffset.UnixEpoch.ToLocalTime().ToString("d MMM"))));
        Assert.That((w7.Gate!.Mark, w7.Gate.IsWaiting, w7.Gate.ToolTip), Is.EqualTo(("◇", true, "Waiting for you to accept the spec")));
        Assert.That(Box(view, "bym-mcp-preconditions").Gate, Is.Null);
    }

    [Test]
    public void AFoldedBox_ShowsOneAction_ItsStatuses()
    {
        var view = Bym();

        Assert.That(Box(view, "bym-mcp-gui-search-reads").PrimaryAction?.Label, Does.StartWith("⧉ Focus"), "its session asks: answer it there");
        Assert.That(Box(view, Spec2).Status.State, Is.Not.EqualTo(StageNodeState.Stuck), "a spec kept outside its records is not Stuck");
        Assert.That(Box(view, Spec2).Gate!.ToolTip, Does.StartWith("Not passed here: no spec.md in its records"));
        Assert.That(Box(view, "bym-mcp-identity").PrimaryAction, Is.Null, "Theseus starts its check: nothing for him to press (git-22, 2026-11-23)");
        Assert.That(Box(view, "bym-mcp-preconditions").HasPrimaryAction, Is.False, "answered: nothing for you");
    }

    [Test]
    public void ARunOfFinishedPieces_FoldsToOneLine_AndTheRestShowAsBoxes()
    {
        var view = Theseus();
        var round = view.Rounds.Single();
        var run = round.Items.OfType<FinishedRunViewModel>().First();

        Assert.That(run.Text, Is.EqualTo($"▶ {run.Boxes.Count} finished"));
        Assert.That(run.Boxes.Select(box => box.Name).Take(2), Is.EqualTo(new[] { "method-kit", "holdout-scenarios" }));
        Assert.That(run.Boxes, Has.All.Matches<PieceNodeViewModel>(box => box.State is StageNodeState.Done or StageNodeState.Answered));
        Assert.That(round.Items.OfType<PieceNodeViewModel>(), Has.None.Matches<PieceNodeViewModel>(box => box.Name == "method-kit"));

        run.ToggleCommand.Execute(null);

        Assert.That((run.IsOpen, run.Text), Is.EqualTo((true, $"▼ {run.Boxes.Count} finished")));
    }
}
