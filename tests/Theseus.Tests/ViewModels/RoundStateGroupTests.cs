using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ClarityFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A round's pieces in Rounds and work, each under the heading of its state, as the count chips name them. "▼ 8
/// finished" was followed by built pieces not yet checked with no heading of their own, so they read as finished, and
/// pieces whose reports Nigel had confirmed sat folded under the card of their session chain (git-22, 2026-11-24).
/// </summary>
[TestFixture]
public class RoundStateGroupTests
{
    private static readonly StageNodeState[] ChipOrder =
    [
        StageNodeState.Done, StageNodeState.Busy, StageNodeState.WaitingOnYou, StageNodeState.HoldoutsNotRun,
        StageNodeState.Stuck, StageNodeState.ToDo, StageNodeState.Answered, StageNodeState.HandedOn,
        StageNodeState.RecordsElsewhere, StageNodeState.NotInProject,
    ];

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

    private ProjectViewModel Bym() => ProjectViewModel.Build(WriteBym(_folder), BymPieces(_folder),
        ProjectViewFixtures.LinksOn(BymLinks(), BymBoard()), [], _views.Settings, _views.Actions);

    private static IReadOnlyList<RoundStateGroupViewModel> Groups(RoundViewModel round) =>
        [.. round.Items.Cast<RoundStateGroupViewModel>()];

    [Test]
    public void EveryBoxOfARound_SitsUnderItsStatesHeading_InTheChipsOrder_NotUnderItsChain()
    {
        foreach (var round in Bym().Rounds)
        {
            var groups = Groups(round);

            Assert.That(groups.Select(group => group.State), Is.Unique);
            Assert.That(groups.Select(group => Array.IndexOf(ChipOrder, group.State)), Is.Ordered);
            Assert.That(groups, Has.All.Matches<RoundStateGroupViewModel>(group => group.Boxes.All(box => box.State == group.State)));
            Assert.That(groups.SelectMany(group => group.Boxes), Is.EquivalentTo(round.Boxes),
                "a piece another handed work to is listed in its own state, not beneath the piece that handed it on");
        }
    }

    [Test]
    public void AHeading_IsItsChipsMarkCountAndWords_DoneFoldedAndTheRestOpen()
    {
        var groups = Bym().Rounds.SelectMany(Groups).ToList();
        var done = groups.First(group => group.State == StageNodeState.Done);
        var built = groups.First(group => group.State == StageNodeState.HoldoutsNotRun);

        Assert.That(done.Text, Is.EqualTo($"▶ ✓ {done.Boxes.Count} done"));
        Assert.That(built.Text, Is.EqualTo($"▼ ◐ {built.Boxes.Count} not yet checked"));
        Assert.That(groups.Where(group => group.State != StageNodeState.Done).Select(group => group.IsOpen), Has.All.True);
        Assert.That(done.Titles, Is.EqualTo(string.Join(" · ", done.Boxes.Select(box => box.Title))), "folded, it names what it holds (G135)");
    }

    [Test]
    public void AHeadingsFold_IsRemembered_AndCollapseAllAndExpandAllIncludeIt()
    {
        var view = Bym();
        var built = view.Rounds.SelectMany(Groups).First(group => group.State == StageNodeState.HoldoutsNotRun);

        built.ToggleCommand.Execute(null);

        Assert.That(Bym().Rounds.SelectMany(Groups).First(group => group.State == StageNodeState.HoldoutsNotRun).IsOpen, Is.False);

        view.ExpandAllCommand.Execute(null);
        Assert.That(view.Rounds.SelectMany(Groups).Select(group => group.IsOpen), Has.All.True);

        view.CollapseAllCommand.Execute(null);
        Assert.That(view.Rounds.SelectMany(Groups).Select(group => group.IsOpen), Has.All.False);
    }

    [Test]
    public void SelectingABoxInAFoldedHeading_OpensIt()
    {
        var view = Bym();
        var done = view.Rounds.SelectMany(Groups).First(group => group.State == StageNodeState.Done);

        view.Select(done.Boxes[0]);

        Assert.That(done.IsOpen, Is.True, "Properties never shows a box that is not on screen");
    }
}
