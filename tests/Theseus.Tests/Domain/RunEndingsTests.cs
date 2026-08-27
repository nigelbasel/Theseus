using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Sessions;

namespace Theseus.Tests.Domain;

[TestFixture]
public class RunEndingsTests
{
    private static readonly DateTimeOffset WatchingSince = new(2026, 8, 27, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = WatchingSince.AddHours(2);

    private FakeRunLog _log = null!;
    private RunEndings _endings = null!;

    [SetUp]
    public void SetUp()
    {
        _log = new FakeRunLog();
        _endings = new RunEndings(WatchingSince);
    }

    [Test]
    public void Reconcile_RecordsAnEndingWhenASessionItWatchedDisappearsFromTheRegistry()
    {
        _log.Append(Run("session-1", WatchingSince.AddMinutes(10)));

        _endings.Reconcile(_log, [Session("session-1")], Now);
        _endings.Reconcile(_log, [], Now);

        var end = _log.Endings.Single();

        Assert.That(end.EndedUtc, Is.EqualTo(Now), "the moment it was seen to vanish is the closest available");
        Assert.That(end.Reason, Is.EqualTo(SessionExitReason.CrashedOrKilled));
        Assert.That(end.ExitCode, Is.Null);
    }

    [Test]
    public void Reconcile_RecordsAnEndingWithNoMomentForARunThatWasAlreadyOverWhenTheseusStarted()
    {
        _log.Append(Run("session-1", WatchingSince.AddHours(-3)));

        _endings.Reconcile(_log, [], Now);

        var end = _log.Endings.Single();

        Assert.That(end.Reason, Is.EqualTo(SessionExitReason.TheseusWasDown));
        Assert.That(end.EndedUtc, Is.Null, "nothing observed the ending, so no time may be claimed for it");
    }

    [Test]
    public void Reconcile_LeavesARunAloneWhileItsSessionIsStillInTheRegistry()
    {
        _log.Append(Run("session-1", WatchingSince.AddHours(-3)));

        _endings.Reconcile(_log, [Session("session-1")], Now);

        Assert.That(_log.Endings, Is.Empty, "a running session is never given an ending, however old");
    }

    [Test]
    public void Reconcile_LeavesAJustLaunchedRunAloneUntilItsSessionRegisters()
    {
        _log.Append(Run("session-1", WatchingSince.AddMinutes(30)));

        _endings.Reconcile(_log, [], Now);

        Assert.That(_log.Endings, Is.Empty,
            "the gap between opening a tab and the registry entry appearing is not a death");
    }

    [Test]
    public void Reconcile_DoesNotEndARunTheShimAlreadyReported()
    {
        var run = Run("session-1", WatchingSince.AddHours(-3)) with
        {
            EndedUtc = Now.AddHours(-1),
            ExitCode = 0,
            ExitReason = SessionExitReason.ClosedCleanly,
        };

        _log.Append(run);
        _endings.Reconcile(_log, [], Now);

        Assert.That(_log.Endings, Is.Empty, "an exit code observed by the shim is never replaced by a guess");
    }

    [Test]
    public void Reconcile_DoesNotEndTheSameRunTwiceAcrossSuccessiveSnapshots()
    {
        _log.Append(Run("session-1", WatchingSince.AddHours(-3)));

        _endings.Reconcile(_log, [], Now);
        _endings.Reconcile(_log, [], Now.AddMinutes(1));

        Assert.That(_log.Endings, Has.Count.EqualTo(1),
            "the registry watcher fires repeatedly; the log must not grow an ending per tick");
    }

    [Test]
    public void Reconcile_ReturnsTheRunsWithTheEndingsItJustWroteAlreadyFoldedIn()
    {
        _log.Append(Run("session-1", WatchingSince.AddHours(-3)));

        Assert.That(_endings.Reconcile(_log, [], Now).Single().HasEnded, Is.True);
    }

    private static SessionRun Run(string sessionId, DateTimeOffset started) =>
        new(Guid.NewGuid(), sessionId, "contact-forms", @"C:\work", null, SessionLaunchMode.FirstRun,
            "Do the thing.", "Contact forms", "#208DD7", started, null, null, null, null,
            SessionExitReason.Unknown);

    private static LiveSession Session(string id) =>
        new(1234, id, @"C:\work", "Contact forms", "busy", "cli", DateTimeOffset.UtcNow);

    private sealed class FakeRunLog : IRunLog
    {
        private readonly List<SessionRun> _runs = [];

        public List<RunEnd> Endings { get; } = [];

        public void Append(SessionRun run) => _runs.Add(run);

        public void AppendEnd(RunEnd end) => Endings.Add(end);

        public IReadOnlyList<SessionRun> Read() =>
        [
            .. _runs.Select(run => Endings.FirstOrDefault(end => end.RunId == run.Id) is { } end
                ? run with { EndedUtc = end.EndedUtc, ExitCode = end.ExitCode, ExitReason = end.Reason }
                : run)
        ];
    }
}
