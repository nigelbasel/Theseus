using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A card's and a selection's Start and Stop as the shell runs them (1.1e AC 10 to 12; review of PR 2, finding 4): asked
/// about in a running project, Cancel doing nothing; marked stopped by you and cleared by a start; ▶ on a done card asking
/// for the instruction and giving it to that start only; ▶ on a project-stopped card starting its project.
/// </summary>
[TestFixture]
public class CardRunCommandsTests
{
    private BoardFixture _board = null!;
    private MemorySettingsStore _settings = null!;
    private readonly List<string> _asked = [];
    private readonly List<(string Key, string? Instruction, string Trigger)> _started = [];
    private readonly List<string> _stopped = [];
    private bool _answer;
    private string? _instruction;
    private ProjectCardViewModel _project = null!;

    [SetUp]
    public void SetUp()
    {
        _board = new BoardFixture().With("member").With("other").With("ad-hoc");
        _board.Board.Refresh();
        _settings = new MemorySettingsStore();
        _asked.Clear();
        _started.Clear();
        _stopped.Clear();
        _answer = true;
        _instruction = null;
        _project = new ProjectCardViewModel("p", "Project P") { IsLive = true, MemberKeys = ["member", "other"] };
    }

    private CardRunCommands Commands()
    {
        var runner = new ProjectRunner(_board.Board.Card, _settings,
            workstreams => _started.AddRange(workstreams.Select(workstream => (workstream.Key, workstream.StartInstruction, "project"))),
            cards => _stopped.AddRange(cards.Select(card => card.Key)), _ => { });

        return new CardRunCommands(_settings, () => [_project], runner,
            (question, verb) =>
            {
                _asked.Add($"{verb}: {question}");

                return _answer;
            },
            (_, _, _) => _instruction,
            (workstreams, trigger) => _started.AddRange(workstreams.Select(workstream => (workstream.Key, workstream.StartInstruction, trigger))),
            cards => _stopped.AddRange(cards.Select(card => card.Key)));
    }

    private WorkstreamCardViewModel Live(string key)
    {
        var card = _board.Card(key)!;
        card.Lifecycle = SessionLifecycle.Running;

        return card;
    }

    [Test]
    public void StoppingACardOfARunningProject_Asks_AndMarksItStoppedByYou()
    {
        Assert.That(Commands().StopCard(Live("member")), Is.True);

        Assert.That(_asked.Single(), Is.EqualTo("Stop: member is part of Project P, which decides what runs. Stop it anyway?"));
        Assert.That(_settings.Read().CardsStoppedByOwner, Is.EqualTo(new[] { "member" }));
        Assert.That(_settings.Read().ProjectRunStates, Is.Empty, "the project's run state is not touched");
    }

    [Test]
    public void Cancel_StopsNothing_AndMarksNothing()
    {
        _answer = false;

        Assert.That(Commands().StopCard(Live("member")), Is.False);
        Assert.That(_settings.Read().CardsStoppedByOwner, Is.Empty);
    }

    [Test]
    public void AnAdHocCard_OrOneOfAStoppedProject_IsNotAsked_NorMarked()
    {
        Assert.That(Commands().StopCard(Live("ad-hoc")), Is.True);

        _project.RunState = ["member"];

        Assert.That(Commands().StopCard(Live("member")), Is.True);
        Assert.That(_asked, Is.Empty);
        Assert.That(_settings.Read().CardsStoppedByOwner, Is.Empty);
    }

    [Test]
    public void StopSelected_AsksAboutTheOwnCards_RecordsTheProjectsRunState_AndStopsEachOnce()
    {
        _project.StopPlan = ProjectStop.For("Project P", []);
        _board.Card("member")!.IsSelected = true;

        Commands().StopSelected([Live("member"), Live("ad-hoc")], []);

        Assert.That(_asked, Has.Count.EqualTo(1));
        Assert.That(_stopped, Is.EqualTo(new[] { "member", "ad-hoc" }));
        Assert.That(_settings.Read().CardsStoppedByOwner, Is.EqualTo(new[] { "member" }));
    }

    [Test]
    public void AStart_TakesTheCardOffTheStoppedByYouList_AndOutOfARunState()
    {
        _settings.Write(_settings.Read() with
        {
            CardsStoppedByOwner = ["member"],
            ProjectRunStates = new Dictionary<string, IReadOnlyList<string>> { ["p"] = ["member", "other"] },
        });

        Commands().Started("member");

        Assert.That(_settings.Read().CardsStoppedByOwner, Is.Empty);
        Assert.That(_settings.Read().ProjectRunStates["p"], Is.EqualTo(new[] { "other" }));
    }

    [Test]
    public void PlayOnADoneCard_AsksForTheInstruction_AndGivesItToThatStartOnly()
    {
        var card = _board.Card("ad-hoc")!;
        card.StopReason = new WhyStopped(WhyStoppedKind.Done, "✓ Done");
        _instruction = "  add the chips  ";

        Commands().StartCard(card);

        Assert.That(_started, Is.EqualTo(new[] { ("ad-hoc", (string?)"add the chips", "RestartForMoreWork") }));
        Assert.That(card.Workstream.StartInstruction, Is.Null, "the card's own workstream never carries it");
    }

    [Test]
    public void PlayOnADoneCardInAProject_StartsIt_AskingNothing()
    {
        var card = _board.Card("ad-hoc")!;
        card.Workstream = card.Workstream with { Project = "elsewhere" };
        card.StopReason = new WhyStopped(WhyStoppedKind.Done, "✓ Done");
        _instruction = "never asked";

        Commands().StartCard(card);

        Assert.That(_started, Is.EqualTo(new[] { ("ad-hoc", (string?)null, "StartCard") }), "sessions-run-the-project item 17");
    }

    [Test]
    public void PlayOnADoneCard_Cancelled_OrBlank_StartsNothing()
    {
        var card = _board.Card("ad-hoc")!;
        card.StopReason = new WhyStopped(WhyStoppedKind.Done, "✓ Done");
        _instruction = "   ";

        Commands().StartCard(card);

        Assert.That(_started, Is.Empty);
    }

    [Test]
    public void PlayOnAProjectStoppedCard_StartsItsProject()
    {
        _project.IsLive = false;
        _project.RunState = ["member", "other"];
        _project.StartPlan = ProjectStart.For("Project P", [], ["member", "other"]);
        var card = _board.Card("member")!;
        card.StopReason = new WhyStopped(WhyStoppedKind.ProjectStopped, "⏸ Project stopped: resumes with Project P");
        var said = new List<string>();
        var runner = new ProjectRunner(_board.Board.Card, _settings, _ => { }, _ => { }, said.Add);
        var commands = new CardRunCommands(_settings, () => [_project], runner, (_, _) => true, null,
            (workstreams, _) => _started.AddRange(workstreams.Select(workstream => (workstream.Key, (string?)null, "card"))), _ => { });

        commands.StartCard(card);

        Assert.That(_started, Is.Empty, "not the card alone");
        Assert.That(said.Single(), Does.StartWith("Not started: member"), "the project's Start ran, for both its cards");
    }

    [Test]
    public void StartSelected_LeavesACardItsProjectCovers_OutOfTheQuestion()
    {
        _project.IsLive = true;

        Commands().StartSelected([_board.Card("member")!], [_board.Card("member")!.Workstream], [_project]);

        Assert.That(_asked, Is.Empty);
    }
}
