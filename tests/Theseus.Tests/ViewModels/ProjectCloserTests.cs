using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Duties;
using Theseus.Core.Projects;
using Theseus.Tests.Duties;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Closing and archiving a project (archive-a-project, Nigel 2026-11-26: "there is no way to archive or close a
/// project"): close asks once, naming what runs and what stops, then stops its cards through the project's Stop, pauses
/// its duties and its overnight queue, and puts it away; Reopen undoes exactly that; archive also archives its cards as
/// their sessions end, and never touches a record file.
/// </summary>
[TestFixture]
public class ProjectCloserTests
{
    private const string Host = "bym-mcp-w4-mcp-host-and-staff-plugin";
    private const string Config = "bym-mcp-w2-config-p5-refusal";
    private static readonly DateTimeOffset Nine = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    private TestProjectsFolder _folder = null!;
    private BoardFixture _board = null!;
    private MemorySettingsStore _settings = null!;
    private DutyBook _duties = null!;
    private string _dutyPath = string.Empty;
    private readonly List<string> _stopped = [];
    private readonly List<string> _notices = [];
    private readonly List<(string Caption, string Question, string Action)> _asked = [];
    private readonly HashSet<string> _live = new(StringComparer.OrdinalIgnoreCase);
    private bool _answer = true;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _board = new BoardFixture();
        _settings = new MemorySettingsStore();
        _dutyPath = Path.Combine(Path.GetTempPath(), $"theseus-close-duties-{Guid.NewGuid():N}.json");
        _duties = new DutyBook(new DutyStore(_dutyPath), new DutyTestClock(Nine), TimeZoneInfo.Utc);

        foreach (var (card, _) in BuildYourMarketCards)
        {
            _board.With(card);
        }

        _board.Board.Refresh();
        _stopped.Clear();
        _notices.Clear();
        _asked.Clear();
        _live.Clear();
        _answer = true;
    }

    [TearDown]
    public void TearDown()
    {
        _folder.Dispose();
        File.Delete(_dutyPath);
    }

    private ProjectRunner Runner() => new(_board.Board.Card, _settings, _ => { },
        cards => _stopped.AddRange(cards.Select(card => card.Key)), _notices.Add);

    private ProjectCloser Closer() => new(_settings, _board.Board.Card, project => Runner().Stop(project), _duties,
        card => _board.Board.SetArchived(card, true), keys => _board.Board.Restore(keys),
        (caption, question, action) =>
        {
            _asked.Add((caption, question, action));

            return _answer;
        },
        _notices.Add, () => Nine)
    {
        HasSession = card => _live.Contains(card.Key),
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

    private IReadOnlyList<string> Archived() =>
        [.. _board.Sidecars.Where(sidecar => sidecar.IsArchived).Select(sidecar => sidecar.Key).Order()];

    [Test]
    public void Close_AsksOnce_NamingHowManyCardsRunAndWhatStops()
    {
        _live.UnionWith([Host, Config]);
        SetDuty(Host, "Watch");
        _settings.Settings = _settings.Settings with { Duties = _settings.Settings.Duties with { QueueProjects = [BuildYourMarket] } };

        Closer().Close(Project(), ProjectClosureKind.Closed);

        Assert.That(_asked, Has.Count.EqualTo(1));
        var (caption, question, action) = _asked.Single();
        Assert.That((caption, action), Is.EqualTo(("Close BuildYourMarket MCP server?", "_Close project")));
        Assert.That(question, Does.StartWith("2 cards are running, "));
        Assert.That(question, Does.Contain("each is asked to write its handover and stop."));
        Assert.That(question, Does.Contain("1 duty pauses."));
        Assert.That(question, Does.Contain("Its overnight queue goes off."));
    }

    [Test]
    public void Close_StopsItsRunningCardsAsStopDoes_SoTheirRunStateIsKept()
    {
        _live.UnionWith([Host, Config]);

        Closer().Close(Project(), ProjectClosureKind.Closed);

        Assert.That(_stopped, Is.EquivalentTo(new[] { Host, Config }), "each asked to hand over and stop");
        Assert.That(_settings.Read().ProjectRunStates[BuildYourMarket], Is.EquivalentTo(new[] { Host, Config }));
    }

    [Test]
    public void Close_PausesItsLiveDuties_AndItsQueue_AndPutsItAway()
    {
        var watch = SetDuty(Host, "Watch");
        var owners = SetDuty(Config, "Owner paused");
        _duties.Pause(owners.Id, Duty.Owner, null);
        _settings.Settings = _settings.Settings with { Duties = _settings.Settings.Duties with { QueueProjects = [BuildYourMarket] } };

        Closer().Close(Project(), ProjectClosureKind.Closed);

        Assert.That(_duties.Get(watch.Id)!.PausedBy, Is.EqualTo(Duty.Theseus), "a session cannot lift a pause Theseus holds");
        Assert.That(_settings.Read().Duties.QueueIsOn(BuildYourMarket), Is.False);
        var closure = ProjectClosures.Of(_settings.Read(), BuildYourMarket)!;
        Assert.That((closure.Kind, closure.QueueWasOn, closure.ClosedUtc), Is.EqualTo((ProjectClosureKind.Closed, true, Nine)));
        Assert.That(closure.PausedDuties, Is.EqualTo(new[] { watch.Id }), "the owner's own pause is not the close's to lift");
    }

    [Test]
    public void ClosingIsCancelled_NothingChanges()
    {
        _live.Add(Host);
        var watch = SetDuty(Host, "Watch");
        _answer = false;

        Assert.That(Closer().Close(Project(), ProjectClosureKind.Archived), Is.False);

        Assert.That(_stopped, Is.Empty);
        Assert.That(_duties.Get(watch.Id)!.IsPaused, Is.False);
        Assert.That(_settings.Read().ClosedProjects, Is.Empty);
        Assert.That(Archived(), Is.Empty);
    }

    [Test]
    public void Reopen_PutsItBackAsItWas_DutiesQueueAndRunState_AndStartsNothing()
    {
        _live.Add(Host);
        var watch = SetDuty(Host, "Watch");
        var owners = SetDuty(Config, "Owner paused");
        _duties.Pause(owners.Id, Duty.Owner, null);
        _settings.Settings = _settings.Settings with { Duties = _settings.Settings.Duties with { QueueProjects = [BuildYourMarket] } };
        Closer().Close(Project(), ProjectClosureKind.Closed);
        _live.Clear();

        Assert.That(Closer().Reopen(BuildYourMarket, "BuildYourMarket MCP server"), Is.True);

        Assert.That(ProjectClosures.IsClosed(_settings.Read(), BuildYourMarket), Is.False);
        Assert.That(_duties.Get(watch.Id)!.IsPaused, Is.False);
        Assert.That(_duties.Get(owners.Id)!.PausedBy, Is.EqualTo(Duty.Owner), "the owner's pause stays his");
        Assert.That(_settings.Read().Duties.QueueIsOn(BuildYourMarket), Is.True);
        Assert.That(_settings.Read().ProjectRunStates[BuildYourMarket], Is.EqualTo(new[] { Host }),
            "Start brings back what was running; nothing starts on its own");
    }

    [Test]
    public void Archive_ArchivesItsQuietCardsAtOnce_AndARunningOneOnceItsSessionEnds()
    {
        _live.Add(Host);
        var closer = Closer();
        var project = Project();

        closer.Close(project, ProjectClosureKind.Archived);

        Assert.That(Archived(), Does.Not.Contain(Host), "a card with a session is never archived under it");
        Assert.That(Archived(), Has.Count.EqualTo(BuildYourMarketCards.Length - 1));

        _live.Clear();
        closer.ArchiveDue(BuildYourMarket, project.MemberKeys);

        Assert.That(Archived(), Has.Count.EqualTo(BuildYourMarketCards.Length));
        Assert.That(ProjectClosures.Of(_settings.Read(), BuildYourMarket)!.ArchivedCards, Has.Count.EqualTo(BuildYourMarketCards.Length));
    }

    [Test]
    public void ReopeningAnArchive_RestoresTheCardsItArchived_ButNotOneArchivedBefore()
    {
        _board.Archive(Config);
        _board.Board.Refresh();

        Closer().Close(Project(), ProjectClosureKind.Archived);
        Closer().Reopen(BuildYourMarket, "BuildYourMarket MCP server");

        Assert.That(Archived(), Is.EqualTo(new[] { Config }));
    }

    [Test]
    public void Archive_KeepsEveryRecordFile_ExactlyAsItWas()
    {
        static IReadOnlyList<(string, DateTime, long)> Files(string folder) =>
        [
            .. Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
                .Select(path => (path, File.GetLastWriteTimeUtc(path), new FileInfo(path).Length)),
        ];

        // The fixture writes the project's files as it builds the pieces, so they are built first.
        var project = Project();
        var before = Files(_folder.Folder);

        Closer().Close(project, ProjectClosureKind.Archived);
        Closer().Reopen(BuildYourMarket, "BuildYourMarket MCP server");

        Assert.That(before, Is.Not.Empty);
        Assert.That(Files(_folder.Folder), Is.EqualTo(before), "project.md and work\\ are never moved, written or deleted");
    }

    [Test]
    public void Archive_SaysWhatItArchives_AndThatTheRecordsStay()
    {
        Closer().Close(Project(), ProjectClosureKind.Archived);

        var (caption, question, action) = _asked.Single();
        Assert.That((caption, action), Is.EqualTo(("Archive BuildYourMarket MCP server?", "_Archive project")));
        Assert.That(question, Does.Contain($"Its {BuildYourMarketCards.Length} cards are archived, each once its session has ended."));
        Assert.That(question, Does.Contain("project.md and its records stay where they are."));
    }

    [Test]
    public void Close_SaysWhereToReopenIt()
    {
        Closer().Close(Project(), ProjectClosureKind.Closed);

        Assert.That(_notices.Last(), Is.EqualTo("Closed BuildYourMarket MCP server. It is under Archived on the board until you reopen it."));
    }

    [Test]
    public void ACardOfTheProjectNoRowNames_SuchAsItsVerifier_IsStoppedAndArchivedWithIt()
    {
        _board.With("verify-host");
        _board.Board.Refresh();
        _live.Add("verify-host");
        var stoppedOthers = new List<string>();
        var closer = new ProjectCloser(_settings, _board.Board.Card, project => Runner().Stop(project), _duties,
            card => _board.Board.SetArchived(card, true), keys => _board.Board.Restore(keys), (_, _, _) => true, _notices.Add, () => Nine)
        {
            HasSession = card => _live.Contains(card.Key),
            OthersOf = _ => ["verify-host"],
            StopCards = cards => stoppedOthers.AddRange(cards.Select(card => card.Key)),
        };
        var project = Project();

        closer.Close(project, ProjectClosureKind.Archived);
        _live.Clear();
        closer.ArchiveDue(BuildYourMarket, project.MemberKeys);

        Assert.That(stoppedOthers, Is.EqualTo(new[] { "verify-host" }));
        Assert.That(Archived(), Does.Contain("verify-host"));
    }
}
