using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A project owns what runs (1.1e AC 8, 9, 11, 13): Stop records the run state, the cards with a session, and stops them;
/// the run state is kept in the settings, so it survives a restart; Start starts exactly it, with no list to tick, and
/// forgets it; a selection acts on a project as a whole and on a card it covers once, through the project.
/// </summary>
[TestFixture]
public class ProjectRunnerTests
{
    private const string Host = "bym-mcp-w4-mcp-host-and-staff-plugin";
    private const string Config = "bym-mcp-w2-config-p5-refusal";

    private TestProjectsFolder _folder = null!;
    private BoardFixture _board = null!;
    private MemorySettingsStore _settings = null!;
    private readonly List<string> _started = [];
    private readonly List<string> _stopped = [];
    private readonly List<string> _notices = [];

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _board = new BoardFixture();
        _settings = new MemorySettingsStore();

        foreach (var (card, _) in BuildYourMarketCards)
        {
            _board.With(card);
        }

        _board.Board.Refresh();
        _started.Clear();
        _stopped.Clear();
        _notices.Clear();
        _launches = true;
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private bool _launches = true;

    /// <summary>The runner as the shell builds it: its start, like StartMany, takes each card it launches out of the run state.</summary>
    private ProjectRunner Runner() => new(_board.Board.Card, _settings,
        workstreams =>
        {
            if (!_launches)
            {
                return;
            }

            _started.AddRange(workstreams.Select(workstream => workstream.Key));
            ProjectRunner.Started(_settings, [.. workstreams.Select(workstream => workstream.Key)]);
        },
        cards => _stopped.AddRange(cards.Select(card => card.Key)),
        _notices.Add);

    private IReadOnlyList<PieceOfWork> Pieces(params string[] running) =>
        BuildYourMarketPieces(_folder, [.. BuildYourMarketBoard().Select(card =>
            running.Contains(card.Key) ? card with { Lifecycle = SessionLifecycle.Running } : card)]);

    /// <summary>The project card as the shell fills it on a refresh, with its run state read from the settings.</summary>
    private ProjectCardViewModel Project(params string[] running)
    {
        var project = new ProjectCardViewModel(BuildYourMarket, "BuildYourMarket MCP server");
        ProjectCardFace.Fill(project, Pieces(running), _settings.Read().ProjectRunStates.GetValueOrDefault(BuildYourMarket));

        return project;
    }

    [Test]
    public void Stop_RecordsTheRunState_ThenStopsThoseSessions()
    {
        Runner().Stop(Project(Host, Config));

        Assert.That(_settings.Read().ProjectRunStates[BuildYourMarket], Is.EquivalentTo(new[] { Host, Config }));
        Assert.That(_stopped, Is.EquivalentTo(new[] { Host, Config }));
    }

    [Test]
    public void AfterARestart_TheStoppedProjectShowsStopped_AndStartStartsExactlyItsRunState_ThenForgetsIt()
    {
        Runner().Stop(Project(Host, Config));

        var reopened = Project();

        Assert.That(reopened.IsStopped, Is.True);
        Assert.That(reopened.StartPlan!.Keys, Is.EquivalentTo(new[] { Host, Config }));

        Runner().Start(reopened);

        Assert.That(_started, Is.EquivalentTo(new[] { Host, Config }), "no list to tick, no other card");
        Assert.That(_settings.Read().ProjectRunStates, Does.Not.ContainKey(BuildYourMarket));
        Assert.That(Project().IsStopped, Is.False);
    }

    [Test]
    public void ACardOfTheRunStateThatCannotStart_IsSkipped_AndNamedInTheStatusBar()
    {
        Runner().Stop(Project(Host, Config));
        var project = Project(Host);

        Runner().Start(project);

        Assert.That(_started, Is.EqualTo(new[] { Config }));
        Assert.That(_notices.Single(), Does.StartWith("Not started: ").And.EndWith("is already running."));
    }

    [Test]
    public void StartSelected_ActsOnAProjectAsAWhole_AndOnACardItCoversOnce()
    {
        Runner().Stop(Project(Host));
        var project = Project();
        var covered = _board.Card(Host)!.Workstream;
        var adHoc = _board.With("ad-hoc").Board;
        adHoc.Refresh();

        Runner().StartSelected([covered, _board.Card("ad-hoc")!.Workstream], [project]);

        Assert.That(_started, Is.EqualTo(new[] { "ad-hoc", Host }), "the covered card once, through its project");
    }

    [Test]
    public void StopSelected_StopsACardInBothOnce_AndRememberRecordsOnlyWhatHasASession()
    {
        var project = Project(Host);
        var runner = Runner();

        var cards = runner.StopSelected([_board.Card(Host)!], [project]);
        runner.Remember([project, Project()]);

        Assert.That(cards.Select(card => card.Key), Is.EqualTo(new[] { Host }));
        Assert.That(_settings.Read().ProjectRunStates.Keys, Is.EqualTo(new[] { BuildYourMarket }));
    }

    [Test]
    public void ANeverStoppedProject_StartsItsFirstCard_WithNothingToForget()
    {
        var project = Project();

        Runner().Start(project);

        Assert.That(_started, Is.EqualTo(project.StartPlan!.Keys));
        Assert.That(_started, Has.Count.EqualTo(1));
        Assert.That(_settings.Read().ProjectRunStates, Is.Empty);
    }

    [Test]
    public void AStartThatLaunchesNothing_KeepsTheRunState()
    {
        Runner().Stop(Project(Host, Config));
        _launches = false;

        Runner().Start(Project());

        Assert.That(_settings.Read().ProjectRunStates[BuildYourMarket], Is.EquivalentTo(new[] { Host, Config }),
            "a cancelled or refused start loses nothing (review of PR 2, finding 1)");
    }

    [Test]
    public void ACardStartedByHand_ThenTheProjectStoppedAgain_KeepsTheOthers()
    {
        Runner().Stop(Project(Host, Config));
        ProjectRunner.Started(_settings, [Host]);

        Runner().Stop(Project(Host));

        Assert.That(_settings.Read().ProjectRunStates[BuildYourMarket], Is.EquivalentTo(new[] { Host, Config }),
            "review of PR 2, finding 2");
    }

    [Test]
    public void WhenEveryCardOfTheRunStateRunsAlready_StartClearsIt()
    {
        Runner().Stop(Project(Host));

        Runner().Start(Project(Host));

        Assert.That(_settings.Read().ProjectRunStates, Is.Empty);
        Assert.That(_started, Is.Empty);
    }

    [Test]
    public void AProjectWithNothingToStart_SaysWhyInTheStatusBar()
    {
        var project = new ProjectCardViewModel(BuildYourMarket, "BuildYourMarket MCP server");
        ProjectCardFace.Fill(project, []);

        Runner().Start(project);

        Assert.That(_notices, Is.EqualTo(new[] { "No card of BuildYourMarket MCP server is on the board" }));
    }
}
