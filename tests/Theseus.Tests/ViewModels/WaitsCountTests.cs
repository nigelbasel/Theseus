using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The sentence, the "waiting on you" chip and the Waits for you heading count the same things: what asks something of
/// the owner. Nigel saw "Nothing waits for you", "◆ 0 waiting on you" and "◆ Waits for you (1)" over "Checking one
/// build…" at once (git-22, 2026-11-24).
/// </summary>
[TestFixture]
public class WaitsCountTests
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

    [Test]
    public void WhatTheseusIsDoing_IsNotAWait_AndIsSaidApart()
    {
        var without = _views.BuildYourMarket(underway: []);
        var view = _views.BuildYourMarket(underway: ["Checking one build…"]);

        Assert.That(view.Waits.Select(wait => wait.Text), Has.None.EqualTo("Checking one build…"));
        Assert.That(view.WaitsHeading, Is.EqualTo(without.WaitsHeading));
        Assert.That(view.Sentence, Is.EqualTo(without.Sentence));
        Assert.That(view.UnderwayText, Is.EqualTo("Theseus is doing this itself: Checking one build…"));
        Assert.That(without.UnderwayText, Is.Empty);
    }

    [Test]
    public void TheChip_TheHeading_AndTheSentence_CountTheSameWaits()
    {
        var view = _views.BuildYourMarket(underway: ["Checking one build…"], otherWaits: pieces =>
        [
            new WaitItem(WaitKind.ConfirmReport, "A check reported: read it and confirm or reject it on the board.",
                [pieces.First(piece => piece.Status.State == StageNodeState.Done).Name]),
        ]);
        var count = view.Waits.Count;

        Assert.That(count, Is.GreaterThan(0));
        Assert.That(view.WaitsHeading, Is.EqualTo($"◆ Waits for you ({count})"));
        Assert.That(view.Counts.Single(chip => chip.State == StageNodeState.WaitingOnYou).Count, Is.EqualTo(count));
        Assert.That(view.Sentence, Does.Contain(count == 1 ? "one thing waits for you" : $" things wait for you"));
        Assert.That(view.Overview.Waits, Has.Count.EqualTo(count));
    }

    [Test]
    public void TheWaitingChip_ShowsTheBoxesAWaitNames_WhateverTheirState()
    {
        string? named = null;
        var view = _views.BuildYourMarket(otherWaits: pieces =>
        {
            named = pieces.First(piece => piece.Status.State == StageNodeState.Done).Name;

            return [new WaitItem(WaitKind.ConfirmReport, "A check reported: confirm it.", [named])];
        });

        view.ToggleFilter(StageNodeState.WaitingOnYou, add: false);

        Assert.That(view.AllNodes.Single(node => node.Name == named).IsShown, Is.True,
            "the chip counts the report waiting on this done box, so its filter shows the box");
    }
}
