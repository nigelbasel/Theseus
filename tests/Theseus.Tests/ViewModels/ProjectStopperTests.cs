using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Duties;
using Theseus.Core.Locks;
using Theseus.Core.Projects;
using Theseus.Tests.Duties;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Stop the project (project-view-as-tabs AC 10, 11; Nigel 2026-11-26: "tell the project to stop and tell all its
/// workstreams to stop and release their locks"): one confirmation; its running cards stop as Stop does; its duties and
/// queue pause; each card's session holds and queue places are released only once its session has ended, never a
/// process hold; Start resumes what the stop paused and nothing else. The project stays open.
/// </summary>
[TestFixture]
public class ProjectStopperTests
{
    private const string Host = "bym-mcp-w4-mcp-host-and-staff-plugin";
    private const string Config = "bym-mcp-w2-config-p5-refusal";
    private static readonly DateTimeOffset Nine = new(2026, 11, 26, 21, 0, 0, TimeSpan.Zero);

    private TestProjectsFolder _folder = null!;
    private BoardFixture _board = null!;
    private MemorySettingsStore _settings = null!;
    private DutyBook _duties = null!;
    private string _dutyPath = string.Empty;
    private readonly List<string> _stopped = [];
    private readonly List<string> _released = [];
    private readonly List<(string Caption, string Question, string Action)> _asked = [];
    private readonly HashSet<string> _live = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LockTicket> _tickets = [];
    private bool _answer = true;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _board = new BoardFixture();
        _settings = new MemorySettingsStore();
        _dutyPath = Path.Combine(Path.GetTempPath(), $"theseus-stop-duties-{Guid.NewGuid():N}.json");
        _duties = new DutyBook(new DutyStore(_dutyPath), new DutyTestClock(Nine), TimeZoneInfo.Utc);

        foreach (var (card, _) in BuildYourMarketCards)
        {
            _board.With(card);
        }

        _board.Board.Refresh();
        _stopped.Clear();
        _released.Clear();
        _asked.Clear();
        _live.Clear();
        _tickets.Clear();
        _answer = true;
    }

    [TearDown]
    public void TearDown()
    {
        _folder.Dispose();
        File.Delete(_dutyPath);
    }

    [Test]
    public void Stop_AsksOnce_NamingTheCardsTheHoldsAndThatNothingCloses()
    {
        _live.UnionWith([Host, Config]);
        SetDuty(Host, "Watch");
        Hold(Host, HoldMode.Session, granted: true);
        Hold(Config, HoldMode.Process, granted: false);
        Hold(Config, HoldMode.Process, granted: true);

        Stopper().Stop(Project());

        Assert.That(_asked, Has.Count.EqualTo(1));
        var (caption, question, action) = _asked.Single();
        Assert.Multiple(() =>
        {
            Assert.That((caption, action), Is.EqualTo(("Stop BuildYourMarket MCP server?", "_Stop project")));
            Assert.That(question, Does.StartWith("2 cards are running, "));
            Assert.That(question, Does.Contain("1 duty pauses."));
            Assert.That(question, Does.Contain("1 lock hold and 1 place in a lock queue are released once each card has stopped."));
            Assert.That(question, Does.Contain("1 test run keeps its lock until it ends."));
            Assert.That(question, Does.Contain("Nothing is closed"));
        });
    }

    [Test]
    public void Stop_Cancelled_DoesNothing()
    {
        _live.Add(Host);
        SetDuty(Host, "Watch");
        _answer = false;

        var stopped = Stopper().Stop(Project());

        Assert.Multiple(() =>
        {
            Assert.That(stopped, Is.False);
            Assert.That(_stopped, Is.Empty);
            Assert.That(_duties.Live.Single().IsPaused, Is.False);
            Assert.That(ProjectStops.Of(_settings.Read(), BuildYourMarket), Is.Null);
        });
    }

    [Test]
    public void Stop_StopsItsRunningCards_PausesItsDutiesAsTheseus_AndItsQueue()
    {
        _live.UnionWith([Host, Config]);
        var duty = SetDuty(Host, "Watch");
        _settings.Settings = _settings.Settings with { Duties = _settings.Settings.Duties with { QueueProjects = [BuildYourMarket] } };

        Stopper().Stop(Project());

        Assert.Multiple(() =>
        {
            Assert.That(_stopped, Is.EquivalentTo(new[] { Host, Config }));
            Assert.That(_duties.Get(duty.Id)!.PausedBy, Is.EqualTo(Duty.Theseus));
            Assert.That(_settings.Read().Duties.QueueIsOn(BuildYourMarket), Is.False);
            Assert.That(ProjectClosures.IsClosed(_settings.Read(), BuildYourMarket), Is.False);
        });
    }

    [Test]
    public void Stop_ReleasesNothingForACardWhoseSessionIsStillOpen()
    {
        _live.Add(Host);
        Hold(Host, HoldMode.Session, granted: true);

        var stopper = Stopper();
        stopper.Stop(Project());
        stopper.ReleaseDue(BuildYourMarket);

        Assert.Multiple(() =>
        {
            Assert.That(_released, Is.Empty);
            Assert.That(ProjectStops.Of(_settings.Read(), BuildYourMarket)!.PendingLockCards, Does.Contain(Host));
        });
    }

    [Test]
    public void ReleaseDue_ReleasesACardsLocksOnceItsSessionHasEnded_Once()
    {
        _live.Add(Host);
        Hold(Host, HoldMode.Session, granted: true);
        var stopper = Stopper();
        stopper.Stop(Project());

        _live.Remove(Host);
        stopper.ReleaseDue(BuildYourMarket);
        stopper.ReleaseDue(BuildYourMarket);

        Assert.Multiple(() =>
        {
            Assert.That(_released, Is.EqualTo(new[] { Host }));
            Assert.That(ProjectStops.Of(_settings.Read(), BuildYourMarket)!.PendingLockCards, Does.Not.Contain(Host));
        });
    }

    [Test]
    public void Stop_ReleasesAQuietCardsQueuePlaceAtOnce()
    {
        Hold(Config, HoldMode.Process, granted: false);

        Stopper().Stop(Project());

        Assert.That(_released, Is.EqualTo(new[] { Config }));
    }

    [Test]
    public void Resume_ResumesOnlyTheDutiesTheStopPaused_AndTheQueue()
    {
        _live.Add(Host);
        var ownersPause = SetDuty(Host, "Owner's");
        _duties.Pause(ownersPause.Id, "owner", "his own pause");
        var watched = SetDuty(Host, "Watch");
        _settings.Settings = _settings.Settings with { Duties = _settings.Settings.Duties with { QueueProjects = [BuildYourMarket] } };
        var stopper = Stopper();
        stopper.Stop(Project());

        stopper.Resume(BuildYourMarket);

        Assert.Multiple(() =>
        {
            Assert.That(_duties.Get(watched.Id)!.IsPaused, Is.False);
            Assert.That(_duties.Get(ownersPause.Id)!.IsPaused, Is.True);
            Assert.That(_settings.Read().Duties.QueueIsOn(BuildYourMarket), Is.True);
            Assert.That(ProjectStops.Of(_settings.Read(), BuildYourMarket), Is.Null);
        });
    }

    [Test]
    public void CanStop_IsTrueWhileACardRunsOrADutyIsLive_FalseOnceAllIsQuiet()
    {
        var stopper = Stopper();

        Assert.That(stopper.CanStop(Project()), Is.False);

        SetDuty(Host, "Watch");

        Assert.That(stopper.CanStop(Project()), Is.True);
    }

    private ProjectRunner Runner() => new(_board.Board.Card, _settings, _ => { },
        cards => _stopped.AddRange(cards.Select(card => card.Key)), _ => { });

    private ProjectStopper Stopper() => new(_settings, _board.Board.Card, project => Runner().Stop(project), _duties,
        (caption, question, action) =>
        {
            _asked.Add((caption, question, action));

            return _answer;
        },
        _ => { }, () => Nine)
    {
        HasSession = card => _live.Contains(card.Key),
        Tickets = () => _tickets,
        Release = (card, _) =>
        {
            _released.Add(card);
            _tickets.RemoveAll(ticket => ticket.Card == card && CardLocks.Releasable(ticket));

            return 1;
        },
    };

    private ProjectCardViewModel Project()
    {
        var project = new ProjectCardViewModel(BuildYourMarket, "BuildYourMarket MCP server");
        var pieces = BuildYourMarketPieces(_folder, [.. BuildYourMarketBoard().Select(card =>
            _live.Contains(card.Key) ? card with { Lifecycle = SessionLifecycle.Running } : card)]);
        ProjectCardFace.Fill(project, pieces, _settings.Read().ProjectRunStates.GetValueOrDefault(BuildYourMarket));

        return project;
    }

    private Duty SetDuty(string card, string name) =>
        _duties.Set(card, name, "check", DutyCadence.Every(TimeSpan.FromHours(1)), DutyEnd.Never, null).Duty!;

    private void Hold(string card, HoldMode mode, bool granted) =>
        _tickets.Add(new LockTicket(Guid.NewGuid().ToString("N"), LockNames.Tests, card, "Work", mode, card + mode + granted, Nine, Nine,
            GrantedUtc: granted ? Nine : null, Card: card));
}
