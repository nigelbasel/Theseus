using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Core.Sessions;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Starting and stopping a whole project from its card, its menu and Start selected / Stop selected (1.1d AC 43;
/// decision 4, Nigel 2026-11-21): Start asks first, then starts only what is ticked and can still start, through the
/// board's own Start; nothing on the board changes until a card has started, so a cancel or a refused start loses no
/// paused mark; Stop asks each running card, through the board's own Stop.
/// </summary>
[TestFixture]
public class ProjectRunnerTests
{
    private const string Config = "bym-mcp-w2-config-p5-refusal";
    private const string Host = "bym-mcp-w4-mcp-host-and-staff-plugin";

    private TestProjectsFolder _folder = null!;
    private BoardFixture _board = null!;
    private readonly List<Workstream> _started = [];
    private readonly List<string> _stopped = [];
    private readonly List<string> _notices = [];
    private Func<ProjectStartViewModel, bool> _answer = _ => true;
    private Func<WorkstreamCardViewModel, string?> _refusal = _ => null;
    private ProjectStartViewModel? _asked;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _board = new BoardFixture();

        foreach (var (card, _) in BuildYourMarketCards)
        {
            _board.With(card);
        }

        _board.Board.Refresh();
        _started.Clear();
        _stopped.Clear();
        _notices.Clear();
        _answer = _ => true;
        _refusal = _ => null;
        _asked = null;
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    /// <summary>
    /// The runner as the shell builds it: its start, like the shell's StartMany, calls <see cref="StartedMarks.Clear"/> for
    /// each card it launches, and launches nothing when Theseus refuses to start.
    /// </summary>
    private ProjectRunner Runner(bool launches = true) => new(_board.Board.Card,
        question =>
        {
            _asked = question;

            return _answer(question);
        },
        card => _refusal(card),
        workstreams =>
        {
            if (!launches)
            {
                return;
            }

            foreach (var workstream in workstreams)
            {
                _started.Add(workstream);
                StartedMarks.Clear(_board.Board, workstream);
            }
        },
        cards => _stopped.AddRange(cards.Select(card => card.Key)),
        (text, afterStart) => _notices.Add($"{afterStart}:{text}"));

    private IReadOnlyList<PieceOfWork> Pieces(SessionLifecycle host = SessionLifecycle.NotStarted) =>
        BuildYourMarketPieces(_folder, [.. BuildYourMarketBoard().Select(card => card.Key switch
        {
            Host => card with { Lifecycle = host },
            Config => card with { IsPaused = true },
            _ => card,
        })]);

    private ProjectStart Start(SessionLifecycle host = SessionLifecycle.NotStarted) =>
        ProjectStart.For("BuildYourMarket MCP server", Pieces(host));

    private ProjectCardViewModel Project(string name = BuildYourMarket)
    {
        var project = new ProjectCardViewModel(name, "BuildYourMarket MCP server");
        ProjectCardFace.Fill(project, Pieces());

        return project;
    }

    private void Pause(string key) => _board.Card(key)!.Workstream = _board.Card(key)!.Workstream with { IsPaused = true };

    private void TickConfig() => _answer = question =>
    {
        question.Rows.Single(row => row.Entry.Key == Config).IsTicked = true;

        return true;
    };

    [Test]
    public void Start_AsksFirst_ListingEveryCard_ThenStartsOnlyTheTickedOnes()
    {
        Runner().Start(Start(SessionLifecycle.Running));

        Assert.That(_asked!.Rows, Has.Count.EqualTo(9));
        Assert.That(_started.Select(workstream => workstream.Key), Is.EqualTo(new[]
        {
            "bym-mcp-w0-preconditions-inventory", "bym-mcp-w1-identityserver-resources-and-staff-client",
            "bym-mcp-w3-build-read-api", "bym-mcp-w5-infra-hygiene-scripts-and-pipelines", RedirectAnchoring, ClientFixes,
        }), "the paused card stays unticked and the running and done ones are never started");
    }

    [Test]
    public void APausedCardTickedInTheConfirmation_StartsWithItsMarkOff_AndLosesItOnlyOnceStarted()
    {
        Pause(Config);
        TickConfig();

        Runner().Start(Start());

        Assert.That(_started.Single(workstream => workstream.Key == Config).IsPaused, Is.False, "not asked about twice");
        Assert.That(_board.Card(Config)!.IsPaused, Is.False);

        var reopened = _board.Reopen();
        reopened.Refresh();

        Assert.That(reopened.Card(Config)!.IsPaused, Is.False, "the cleared mark is saved");
    }

    [Test]
    public void AStartTheseusRefuses_LeavesThePausedMarkOn()
    {
        Pause(Config);
        TickConfig();

        Runner(launches: false).Start(Start());

        Assert.That(_board.Card(Config)!.IsPaused, Is.True);
    }

    [Test]
    public void CancellingALaterProjectsConfirmation_StartsNothing_AndLeavesTheFirstProjectsPausedCardPaused()
    {
        Pause(Config);
        var answers = 0;
        _answer = question =>
        {
            answers++;
            question.Rows.Single(row => row.Entry.Key == Config).IsTicked = true;

            return answers == 1;
        };

        Runner().StartSelected([], [Project(), Project("another")]);

        Assert.That(answers, Is.EqualTo(2));
        Assert.That(_started, Is.Empty);
        Assert.That(_board.Card(Config)!.IsPaused, Is.True);
    }

    [Test]
    public void ACardThatCanNoLongerStartWhenStartIsPressed_IsLeftOut_AndNamed()
    {
        _refusal = card => card.Key == ClientFixes ? "is running" : null;

        Runner().Start(Start());

        Assert.That(_started.Select(workstream => workstream.Key), Does.Not.Contain(ClientFixes));
        Assert.That(_notices, Is.EqualTo(new[] { $"True:Not started, since the board changed after the list was shown: {ClientFixes} is running." }));
    }

    [Test]
    public void RefusalOf_ReadsTheSessionsAsTheyAreNow_AndTheBlockedMark()
    {
        var card = _board.Card(ClientFixes)!;
        card.Workstream = card.Workstream with { LastSessionId = "s-1" };
        var live = new SessionRegistrySnapshot([new LiveSession(1, "s-1", null, null, null, "cli", DateTimeOffset.UtcNow)], 0, 0);

        Assert.That(ProjectRunner.RefusalOf(card, SessionRegistrySnapshot.Empty), Is.Null);
        Assert.That(ProjectRunner.RefusalOf(card, live), Is.EqualTo("is running"));

        card.Workstream = card.Workstream with { IsBlocked = true };

        Assert.That(ProjectRunner.RefusalOf(card, SessionRegistrySnapshot.Empty), Is.EqualTo("is blocked"));
    }

    [Test]
    public void StartSelected_StartsACardTickedOnTheBoardAndInTheConfirmationOnce_AsTheConfirmationGaveIt()
    {
        Pause(Config);
        TickConfig();
        var onTheBoard = _board.Card(Config)!.Workstream;

        Runner().StartSelected([onTheBoard], [Project()]);

        Assert.That(_started.Where(workstream => workstream.Key == Config).Select(workstream => workstream.IsPaused),
            Is.EqualTo(new[] { false }));
        Assert.That(_started, Has.Count.EqualTo(8));
    }

    [Test]
    public void Stop_AsksEachCardOfTheProjectWithASession_AsACardsStopDoes()
    {
        var project = new ProjectCardViewModel(BuildYourMarket, "BuildYourMarket MCP server");
        ProjectCardFace.Fill(project, Pieces(SessionLifecycle.Running));

        Runner().Stop(project.StopPlan!);

        Assert.That(_stopped, Is.EqualTo(new[] { Host }));
        Assert.That(Runner().StopSelected([_board.Card(Host)!], [project]).Select(card => card.Key), Is.EqualTo(new[] { Host }),
            "Stop selected asks a card once, selected or in a selected project");
    }

    [Test]
    public void TheCardsButtons_AreEnabledByItsPlans_AndStartAlsoByTheLauncher()
    {
        var project = Project();
        var canLaunch = true;
        Runner().Wire(project, () => canLaunch);

        Assert.That((project.StartCommand!.CanExecute(null), project.StopCommand!.CanExecute(null)), Is.EqualTo((true, false)));

        canLaunch = false;

        Assert.That(project.StartCommand.CanExecute(null), Is.False);
    }
}
