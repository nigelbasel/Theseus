using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Persistence;
using Theseus.Core.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The board carrying out a session's calls on its descendants (1.1e AC 14 to 16; review of PR 3): a start with the
/// instruction for that start only, counted only once it launched; a pause marked with the calling card and why, kept
/// across a restart and surviving a project's Start; the state checked again on the board; every call that was done said
/// after the board's own words and kept in the card's History.
/// </summary>
[TestFixture]
public class BoardWorkstreamControlTests
{
    private BoardFixture _board = null!;
    private string _events = string.Empty;
    private readonly List<(string Key, string? Instruction, string Trigger)> _started = [];
    private readonly List<string> _stopped = [];
    private readonly List<string> _notices = [];
    private string? _startRefusal;
    private string? _stopRefusal;
    private int _refreshes;

    [SetUp]
    public void SetUp()
    {
        _board = new BoardFixture().With("child").With("parent");
        _board.Board.Refresh();
        _events = Path.Combine(Path.GetTempPath(), $"theseus-card-events-{Guid.NewGuid():N}.jsonl");
        _started.Clear();
        _stopped.Clear();
        _notices.Clear();
        _startRefusal = null;
        _stopRefusal = null;
        _refreshes = 0;
    }

    [TearDown]
    public void TearDown() => File.Delete(_events);

    private BoardWorkstreamControl Control() => new(_board.Board,
        (workstreams, trigger) =>
        {
            _started.AddRange(workstreams.Select(each => (each.Key, each.StartInstruction, trigger)));
            _notices.Add("the board's own start line");

            return _startRefusal;
        },
        card =>
        {
            _stopped.Add(card.Key);

            return _stopRefusal;
        },
        new CardEventLog(_events),
        _notices.Add,
        () => new DateTimeOffset(2026, 11, 21, 21, 0, 0, TimeSpan.Zero),
        () => _refreshes++);

    private IReadOnlyList<string> Events() => [.. new CardEventLog(_events).For("child").Select(each => each.Text)];

    [Test]
    public void AStart_CarriesTheInstructionForThatStartOnly_AndIsSaidAfterTheBoardsOwnWords()
    {
        Assert.That(Control().Start("child", "more work: the chips\nand the filter", "parent"), Is.Null);

        Assert.That(_started, Is.EqualTo(new[] { ("child", (string?)"more work: the chips\nand the filter", "StartedBySession") }));
        Assert.That(_board.Card("child")!.Workstream.StartInstruction, Is.Null, "the card itself never carries it");
        Assert.That(Events(), Is.EqualTo(new[] { "Started by parent: more work: the chips" }));
        Assert.That(_notices, Is.EqualTo(new[] { "the board's own start line", "child: Started by parent: more work: the chips." }),
            "said after the start, so it is not overwritten (review, finding 4)");
    }

    [Test]
    public void AStartOrResumeWithAnInstruction_IsKeptAsAHandIn_WithWhoGaveItAndAllOfIt()
    {
        // git-22, 2026-11-24: work a lead gives an existing card showed only as the card turning busy.
        Assert.That(Control().Start("child", "more work: the chips\nand the filter", "parent"), Is.Null);

        var handIn = new CardEventLog(_events).LatestHandIns()["child"];

        Assert.That((handIn.By, handIn.Instruction, handIn.AtUtc), Is.EqualTo(((string?)"parent", (string?)"more work: the chips\nand the filter",
            new DateTimeOffset(2026, 11, 21, 21, 0, 0, TimeSpan.Zero))));

        var child = _board.Card("child")!;
        child.Workstream = child.Workstream with { IsPaused = true, PausedBy = "parent" };
        Assert.That(Control().Resume("child", "now the legend", "parent"), Is.Null);
        Assert.That(new CardEventLog(_events).LatestHandIns()["child"].Instruction, Is.EqualTo("now the legend"), "the latest is the one shown");
        Assert.That(Control().Start("parent", null, "child"), Is.Null);
        Assert.That(new CardEventLog(_events).LatestHandIns().ContainsKey("parent"), Is.False, "a start with no instruction hands in nothing");
    }

    [Test]
    public void AStartThatLaunchedNothing_SaysWhy_AndRecordsNothing()
    {
        _startRefusal = "child did not start: a tab of that name is already open; the user is asked";

        Assert.That(Control().Start("child", null, "parent"), Is.EqualTo(_startRefusal));
        Assert.That(Events(), Is.Empty, "review, finding 5");
    }

    [Test]
    public void ThePause_IsMarkedWithTheCallingCard_KeptAcrossARestart_AndShownByItsTitle()
    {
        _board.Card("child")!.Lifecycle = SessionLifecycle.Running;

        Assert.That(Control().Pause("child", "parent", "waiting for the spec"), Is.Null);

        Assert.That((_stopped, _refreshes), Is.EqualTo((new List<string> { "child" }, 1)), "a running card is asked to stop, and the board drawn again");

        var reopened = _board.Reopen();
        reopened.Refresh();
        var card = reopened.Cards.Single(each => each.Key == "child");

        Assert.That((card.Workstream.IsPaused, card.Workstream.PausedBy, card.Workstream.PauseReason),
            Is.EqualTo((true, (string?)"parent", (string?)"waiting for the spec")));
        Assert.That(CardStopReasons.For(card, new Dictionary<string, IReadOnlyList<string>>(), name => name, [], [], key => $"The {key}")!.Line,
            Is.EqualTo("⏸ Paused by The parent: waiting for the spec"));
    }

    [Test]
    public void TheOwnersPause_AndAnotherCardsPause_AreNeverTakenOver()
    {
        var child = _board.Card("child")!;
        child.Workstream = child.Workstream with { IsPaused = true };

        Assert.That(Control().Pause("child", "parent", null), Is.EqualTo("child is paused by the user."), "review, finding 1");
        Assert.That(Control().Resume("child", null, "parent"), Is.EqualTo("child is paused by the user."));

        child.Workstream = child.Workstream with { PausedBy = "other" };

        Assert.That(Control().Pause("child", "parent", null), Is.EqualTo("child is paused by other."));
        Assert.That(Control().Resume("child", null, "parent"), Is.EqualTo("child is not paused by you."));
        Assert.That((_started, Events()), Is.EqualTo((new List<(string, string?, string)>(), Array.Empty<string>())));
    }

    [Test]
    public void InAProject_AnotherSessionsPause_IsTakenOverOrCleared_ButNeverTheOwners()
    {
        var child = _board.Card("child")!;
        var parent = _board.Card("parent")!;
        parent.Workstream = parent.Workstream with { Project = "theseus" };
        child.Workstream = child.Workstream with { Project = "theseus", IsPaused = true, PausedBy = "other" };

        Assert.That(Control().Pause("child", "parent", "mine now"), Is.Null, "sessions-run-the-project item 9");
        Assert.That(_board.Card("child")!.Workstream.PausedBy, Is.EqualTo("parent"));

        child.Workstream = child.Workstream with { IsPaused = true, PausedBy = "other" };

        Assert.That(Control().Resume("child", null, "parent"), Is.Null);
        Assert.That(_board.Card("child")!.Workstream.IsPaused, Is.False);
        Assert.That(Control().Resume("child", null, "parent"), Is.EqualTo("child is not paused."));

        child.Workstream = child.Workstream with { IsPaused = true, PausedBy = null };

        Assert.That(Control().Resume("child", null, "parent"), Is.EqualTo("child is paused by the user."));
        Assert.That(Control().Pause("child", "parent", null), Is.EqualTo("child is paused by the user."));
    }

    [Test]
    public void ACardOutsideTheCallersProject_IsRefusedOnTheBoard_WhateverTheCall()
    {
        var child = _board.Card("child")!;
        var parent = _board.Card("parent")!;
        parent.Workstream = parent.Workstream with { Project = "another" };
        child.Workstream = child.Workstream with { Project = "theseus", IsPaused = true, PausedBy = "other" };
        const string refused = "child is not a card of another: another project's cards and ad hoc cards are the user's to start or stop.";

        Assert.Multiple(() =>
        {
            Assert.That(Control().Resume("child", null, "parent"), Is.EqualTo(refused));
            Assert.That(Control().Start("child", null, "parent"), Is.EqualTo(refused));
            Assert.That(Control().Pause("child", "parent", null), Is.EqualTo(refused));
            Assert.That(Control().Stop("child", "parent"), Is.EqualTo(refused));
            Assert.That(_started, Is.Empty);
        });

        child.Workstream = child.Workstream with { Project = null };

        Assert.That(Control().Start("child", null, "parent"), Is.EqualTo(refused), "moved out to ad hoc (item 11)");
    }

    [Test]
    public void TheBoardChecksTheStateAgain_BlockedRunningOrArchived()
    {
        var child = _board.Card("child")!;
        child.Workstream = child.Workstream with { IsPaused = true, PausedBy = "parent", IsBlocked = true };

        Assert.That(Control().Resume("child", null, "parent"), Is.EqualTo("child is blocked: the user marked it so."), "review, finding 3");

        child.Workstream = child.Workstream with { IsBlocked = false, IsPaused = false, PausedBy = null };
        child.Lifecycle = SessionLifecycle.Running;

        Assert.That(Control().Start("child", null, "parent"), Is.EqualTo("child is running."));
        Assert.That(Control().Start("gone", null, "parent"), Does.StartWith("gone is not on the board just now"));
        Assert.That(_started, Is.Empty);
    }

    [Test]
    public void APausedCard_SurvivesItsProjectsStart()
    {
        var paused = Theseus.Tests.Projects.ProjectViewFixtures.Card("child") with { PausedBy = "Parent" };
        var piece = new PieceOfWork("p", "child", "child", "1.1", "One", PipelineKind.Freeform, null, false, []) { Card = paused, IsCurrentPiece = true };

        var start = ProjectStart.For("Project", [piece], ["child"]);

        Assert.That((start.CanStart, start.Skipped.Single()), Is.EqualTo((false, "child is paused by Parent")));
    }

    [Test]
    public void AResume_ClearsThePause_AndStarts_OrPutsThePauseBackWhenNothingLaunched()
    {
        var control = Control();
        control.Pause("child", "parent", "later");
        _startRefusal = "child did not start: Theseus could not start it just now";

        Assert.That(control.Resume("child", "go on", "parent"), Is.EqualTo(_startRefusal));
        Assert.That((_board.Card("child")!.Workstream.IsPaused, _board.Card("child")!.Workstream.PausedBy, _board.Card("child")!.Workstream.PauseReason),
            Is.EqualTo((true, (string?)"parent", (string?)"later")), "the pause stands (review, finding 5)");

        _startRefusal = null;

        Assert.That(control.Resume("child", "go on", "parent"), Is.Null);
        Assert.That((_board.Card("child")!.Workstream.IsPaused, _board.Card("child")!.Workstream.PausedBy), Is.EqualTo((false, (string?)null)));
        Assert.That(Events(), Is.EqualTo(new[] { "Paused by parent: later", "Resumed by parent: go on" }));
    }

    [Test]
    public void AStop_IsTheStopRequestAlone_AndSaysWhyWhenItCannotBeSent()
    {
        _board.Card("child")!.Lifecycle = SessionLifecycle.Running;

        Assert.That(Control().Stop("child", "parent"), Is.Null);
        Assert.That(Events(), Is.EqualTo(new[] { "Stopped by parent" }));

        _stopRefusal = "Theseus's stop hook is not installed, so child cannot be asked to stop from here; the user must stop it.";

        Assert.That(Control().Stop("child", "parent"), Is.EqualTo(_stopRefusal));
        Assert.That(Events(), Has.Count.EqualTo(1), "nothing recorded for a stop that was not sent");
    }
}
