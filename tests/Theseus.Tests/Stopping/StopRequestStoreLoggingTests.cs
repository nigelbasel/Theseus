using Theseus.Core.Diagnostics;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>
/// What the stop store records in the Theseus log, and how a stop asked for again is treated
/// (resumed-stop, fix pass 2, 2026-11-18).
/// </summary>
[TestFixture]
public class StopRequestStoreLoggingTests
{
    private const string SessionId = "d616ea10-e23d-4b77-8157-c39be7095633";
    private static readonly DateTimeOffset Asked = new(2026, 11, 18, 15, 19, 15, TimeSpan.Zero);

    private string _directory = string.Empty;
    private StopRequestStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-stop-log-{Guid.NewGuid():N}");
        TheseusLog.Start(Path.Combine(_directory, "logs"));
        _store = new StopRequestStore(Path.Combine(_directory, "stop-requests"));
    }

    [TearDown]
    public void TearDown()
    {
        TheseusLog.Stop();

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void Sweep_ReportsAnExpiredStopThatWasNeverClosed()
    {
        _store.Write(Request(Asked));
        _store.Claim(SessionId);

        _store.Sweep(Asked.AddHours(5), finished: _ => false);

        Assert.That(Log(), Does.Contain("Stop of stop-check-stays-idle did not finish (it expired"));
    }

    [Test]
    public void Sweep_DoesNotReportAStopWhoseExitWasAlreadyTyped()
    {
        _store.Write(Request(Asked));
        _store.Claim(SessionId);

        var gone = _store.Sweep(Asked.AddHours(5), finished: _ => true);

        Assert.That(gone, Is.EqualTo(1), "it is still removed");
        Assert.That(Log(), Does.Not.Contain("did not finish"));
    }

    /// <summary>
    /// G3: a request left waiting by an earlier run of the session is not this run's stop. Keeping its time would
    /// make the close step take the new stop for an earlier process's, and never close the session.
    /// </summary>
    [Test]
    public void Write_StartsAFreshStop_WhenTheWaitingOneIsFromAnEarlierRunOfTheSession()
    {
        _store.Write(Request(Asked));
        var resumed = Asked.AddMinutes(3);

        _store.Write(Request(Asked.AddMinutes(4)), sessionStartedUtc: resumed);

        Assert.That(_store.Waiting(SessionId)!.RequestedUtc, Is.EqualTo(Asked.AddMinutes(4)));
        Assert.That(Log(), Does.Contain("Stop of stop-check-stays-idle did not finish (a newer session is running"));
    }

    [Test]
    public void Write_KeepsTheFirstTime_WhenTheWaitingOneIsFromThisRunOfTheSession()
    {
        var started = Asked.AddMinutes(-10);
        _store.Write(Request(Asked));

        _store.Write(Request(Asked.AddSeconds(37)), sessionStartedUtc: started);

        Assert.That(_store.Waiting(SessionId)!.RequestedUtc, Is.EqualTo(Asked));
        Assert.That(Log(), Does.Not.Contain("did not finish"));
    }

    private string Log()
    {
        TheseusLog.Stop();

        return string.Join(Environment.NewLine,
            Directory.GetFiles(Path.Combine(_directory, "logs")).Select(File.ReadAllText));
    }

    private static PendingStopRequest Request(DateTimeOffset requested) =>
        new(SessionId, "stop-check-stays-idle", "Stop check: stays idle", "Write a handoff and stop.",
            requested, requested.AddHours(4));
}
