using Theseus.Core.Launching;
using Theseus.Core.Sessions;

namespace Theseus.Tests.Launching;

[TestFixture]
public class LaunchDecisionServiceTests
{
    private const string ExistingId = "6d6942b6-162a-4da4-a420-b265967ed98a";
    private const string MintedId = "11111111-2222-3333-4444-555555555555";

    private LaunchDecisionService _service = null!;

    [SetUp]
    public void SetUp() => _service = new LaunchDecisionService(new StubSessionIdFactory(MintedId));

    private static SessionRegistrySnapshot NothingLive() => SessionRegistrySnapshot.Empty;

    private static SessionRegistrySnapshot Live(string sessionId, int pid = 40000) =>
        new([new LiveSession(pid, sessionId, @"C:\work\repo", "name", "busy", "cli", DateTimeOffset.UtcNow)], 0, 0);

    [Test]
    public void Decide_RefusesToResumeASessionThatIsStillRunning()
    {
        var request = new LaunchRequest(ExistingId, TranscriptExists: true, WorkingDirectoryExists: true);

        var decision = _service.Decide(request, Live(ExistingId));

        Assert.That(decision.Allowed, Is.False);
        Assert.That(decision.BlockedReason, Is.EqualTo(LaunchBlockedReason.AlreadyLive));
        Assert.That(decision.LiveSessions, Has.Count.EqualTo(1));
    }

    [Test]
    public void Decide_RefusingALiveSession_ReportsEveryProcessHoldingIt()
    {
        var snapshot = new SessionRegistrySnapshot(
        [
            new LiveSession(1, ExistingId, null, null, null, "cli", DateTimeOffset.UtcNow),
            new LiveSession(2, ExistingId, null, null, null, "cli", DateTimeOffset.UtcNow),
        ], 0, 0);
        var request = new LaunchRequest(ExistingId, TranscriptExists: true, WorkingDirectoryExists: true);

        var decision = _service.Decide(request, snapshot);

        Assert.That(decision.Allowed, Is.False);
        Assert.That(decision.LiveSessions.Select(s => s.Pid), Is.EquivalentTo(new[] { 1, 2 }));
    }

    [Test]
    public void Decide_IgnoresOtherLiveSessionsWhenTheyAreNotThisWorkstream()
    {
        var request = new LaunchRequest(ExistingId, TranscriptExists: true, WorkingDirectoryExists: true);

        var decision = _service.Decide(request, Live("a-different-session"));

        Assert.That(decision.Allowed, Is.True);
        Assert.That(decision.Mode, Is.EqualTo(LaunchMode.Resume));
    }

    [Test]
    public void Decide_ResumesWhenTheSessionIsDeadAndItsTranscriptSurvives()
    {
        var request = new LaunchRequest(ExistingId, TranscriptExists: true, WorkingDirectoryExists: true);

        var decision = _service.Decide(request, NothingLive());

        Assert.That(decision.Allowed, Is.True);
        Assert.That(decision.Mode, Is.EqualTo(LaunchMode.Resume));
        Assert.That(decision.SessionId, Is.EqualTo(ExistingId));
    }

    [Test]
    public void Decide_MintsANewSessionWhenTheTranscriptIsMissing()
    {
        var request = new LaunchRequest(ExistingId, TranscriptExists: false, WorkingDirectoryExists: true);

        var decision = _service.Decide(request, NothingLive());

        Assert.That(decision.Allowed, Is.True);
        Assert.That(decision.Mode, Is.EqualTo(LaunchMode.FirstRun));
        Assert.That(decision.SessionId, Is.EqualTo(MintedId));
    }

    [Test]
    public void Decide_MintsANewSessionForAWorkstreamThatHasNeverRun()
    {
        var request = new LaunchRequest(null, TranscriptExists: false, WorkingDirectoryExists: true);

        var decision = _service.Decide(request, NothingLive());

        Assert.That(decision.Allowed, Is.True);
        Assert.That(decision.Mode, Is.EqualTo(LaunchMode.FirstRun));
        Assert.That(decision.SessionId, Is.EqualTo(MintedId));
    }

    [Test]
    public void Decide_RefusesWhenTheWorkingDirectoryHasGone()
    {
        var request = new LaunchRequest(ExistingId, TranscriptExists: true, WorkingDirectoryExists: false);

        var decision = _service.Decide(request, NothingLive());

        Assert.That(decision.Allowed, Is.False);
        Assert.That(decision.BlockedReason, Is.EqualTo(LaunchBlockedReason.WorkingDirectoryMissing));
    }

    [Test]
    public void Decide_PrefersTheLiveSessionWarningOverAMissingDirectory()
    {
        var request = new LaunchRequest(ExistingId, TranscriptExists: true, WorkingDirectoryExists: false);

        var decision = _service.Decide(request, Live(ExistingId));

        Assert.That(decision.BlockedReason, Is.EqualTo(LaunchBlockedReason.AlreadyLive));
    }

    [Test]
    public void Decide_TreatsAnEmptySessionIdAsNeverHavingRun()
    {
        var request = new LaunchRequest("   ", TranscriptExists: true, WorkingDirectoryExists: true);

        var decision = _service.Decide(request, NothingLive());

        Assert.That(decision.Mode, Is.EqualTo(LaunchMode.FirstRun));
        Assert.That(decision.SessionId, Is.EqualTo(MintedId));
    }

    private sealed class StubSessionIdFactory(string value) : ISessionIdFactory
    {
        public string Create() => value;
    }
}
