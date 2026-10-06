using Theseus.Core.Abstractions;
using Theseus.Core.Locks;

namespace Theseus.Tests.Locks;

[TestFixture]
public class LockBookTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private MovableClock _clock = null!;
    private FakeLiveness _liveness = null!;
    private LockBook _book = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-locks-{Guid.NewGuid():N}");
        _clock = new MovableClock(Nine);
        _liveness = new FakeLiveness();
        _book = Open();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void Request_GrantsAFreeLockAtOnce()
    {
        var answer = Ask("Legacy job migration", "PersistenceTests.Jobs");

        Assert.That(answer.Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(answer.ReservedUntilUtc, Is.EqualTo(Nine + LockBook.Reservation));
    }

    [Test]
    public void Request_QueuesBehindTheHolderAndSaysWhoHoldsIt()
    {
        Ask("Legacy job migration", "PersistenceTests.Jobs");

        var answer = Ask("NDC-1190", "Api.Contacts.Tests");

        Assert.That(answer.Status, Is.EqualTo(LockStatus.Waiting));
        Assert.That(answer.Position, Is.EqualTo(1));
        Assert.That(answer.Holder!.Who, Is.EqualTo("Legacy job migration"));
        Assert.That(answer.Detail, Does.Contain("Legacy job migration"));
    }

    [Test]
    public void Request_AskedAgainBySameRequester_KeepsItsPlaceAndToken()
    {
        Ask("Legacy job migration", "PersistenceTests.Jobs");
        var first = Ask("NDC-1190", "Api.Contacts.Tests");
        Ask("GDPR", "Api.Gdpr.Tests");

        var again = Ask("NDC-1190", "Api.Contacts.Tests");

        Assert.That(again.Token, Is.EqualTo(first.Token));
        Assert.That(again.Position, Is.EqualTo(1));
    }

    [Test]
    public void Request_RefusesALockThatDoesNotExist()
    {
        var answer = _book.Request(new LockRequest("sql", "Someone", "Something", HoldMode.Process));

        Assert.That(answer.Status, Is.EqualTo(LockStatus.Refused));
        Assert.That(_book.Snapshot(), Is.Empty);
    }

    [Test]
    public void Request_RefusesASessionHoldWithoutASession()
    {
        var answer = _book.Request(new LockRequest(LockNames.DevStack, "Someone", "AFE", HoldMode.Session));

        Assert.That(answer.Status, Is.EqualTo(LockStatus.Refused));
    }

    [Test]
    public void Locks_AreIndependent()
    {
        Ask("Legacy job migration", "PersistenceTests.Jobs");

        var stack = _book.Request(new LockRequest(LockNames.DevStack, "NDC-679", "AFE editor", HoldMode.Session, SessionId: "s-1"));

        Assert.That(stack.Status, Is.EqualTo(LockStatus.Granted));
    }

    [Test]
    public void Release_HandsTheLockToTheNextInLine()
    {
        var holder = Ask("Legacy job migration", "PersistenceTests.Jobs");
        var waiter = Ask("NDC-1190", "Api.Contacts.Tests");

        _book.Release(holder.Token!);

        Assert.That(_book.Check(waiter.Token!).Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(_book.Check(holder.Token!).Status, Is.EqualTo(LockStatus.Ended));
    }

    [Test]
    public void Attach_TiesTheHoldToTheProcess_AndItsExitReleasesIt()
    {
        var holder = Ask("Legacy job migration", "PersistenceTests.Jobs");
        _liveness.Processes[20036] = "start-1";

        Assert.That(_book.Attach(holder.Token!, 20036).Status, Is.EqualTo(LockStatus.Granted));

        _clock.Advance(TimeSpan.FromHours(3));
        Assert.That(_book.Check(holder.Token!).Status, Is.EqualTo(LockStatus.Granted), "an attached hold has no time limit");

        var waiter = Ask("NDC-1190", "Api.Contacts.Tests");
        _liveness.Processes.Remove(20036);

        Assert.That(_book.Check(waiter.Token!).Status, Is.EqualTo(LockStatus.Granted));
    }

    [Test]
    public void Attach_AReusedProcessIdIsNotTheHolder()
    {
        var holder = Ask("Legacy job migration", "PersistenceTests.Jobs");
        _liveness.Processes[20036] = "start-1";
        _book.Attach(holder.Token!, 20036);

        _liveness.Processes[20036] = "start-2";

        Assert.That(_book.Check(holder.Token!).Status, Is.EqualTo(LockStatus.Ended));
    }

    [Test]
    public void Attach_RefusesAWaiter()
    {
        Ask("Legacy job migration", "PersistenceTests.Jobs");
        var waiter = Ask("NDC-1190", "Api.Contacts.Tests");
        _liveness.Processes[1] = "start";

        Assert.That(_book.Attach(waiter.Token!, 1).Status, Is.EqualTo(LockStatus.Refused));
    }

    [Test]
    public void AGrantNobodyTakesUp_PassesOnAfterTheReservation()
    {
        var holder = Ask("Legacy job migration", "PersistenceTests.Jobs");
        var waiter = Ask("NDC-1190", "Api.Contacts.Tests");

        _clock.Advance(LockBook.Reservation + TimeSpan.FromSeconds(1));

        Assert.That(_book.Check(waiter.Token!).Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(_book.Check(holder.Token!).Status, Is.EqualTo(LockStatus.Ended));
    }

    [Test]
    public void AWaiterThatStopsAsking_IsDroppedAfterTheGrace()
    {
        HoldWithProcess("Legacy job migration", "PersistenceTests.Jobs", 20036);
        var quiet = Ask("NDC-1190", "Api.Contacts.Tests");
        var keen = Ask("GDPR", "Api.Gdpr.Tests");

        for (var minute = 0; minute < 6; minute++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
            _book.Check(keen.Token!);
        }

        Assert.That(_book.Check(quiet.Token!).Status, Is.EqualTo(LockStatus.Ended));
        Assert.That(_book.Check(keen.Token!).Position, Is.EqualTo(1));
    }

    [Test]
    public void ASessionHold_EndsOnlyAfterTheSessionIsMissedTwice()
    {
        _liveness.Sessions.Add("s-1");
        var hold = _book.Request(new LockRequest(LockNames.DevStack, "NDC-679", "AFE editor", HoldMode.Session, SessionId: "s-1"));

        _liveness.Sessions.Remove("s-1");
        _book.Sweep();
        Assert.That(_book.Check(hold.Token!).Status, Is.EqualTo(LockStatus.Ended), "the Check is the second miss");

        _liveness.Sessions.Add("s-2");
        var other = _book.Request(new LockRequest(LockNames.DevStack, "NDC-690", "AFE", HoldMode.Session, SessionId: "s-2"));
        _liveness.Sessions.Remove("s-2");
        _book.Sweep();
        _liveness.Sessions.Add("s-2");

        Assert.That(_book.Check(other.Token!).Status, Is.EqualTo(LockStatus.Granted), "one missed read is not an exit");
    }

    [Test]
    public void ASessionHoldingTwoLocks_IsMissedOncePerSweepNotOncePerLock()
    {
        _liveness.Sessions.Add("s-1");
        var stack = _book.Request(new LockRequest(LockNames.DevStack, "NDC-679", "AFE", HoldMode.Session, SessionId: "s-1"));
        _book.Request(new LockRequest(LockNames.Llama, "NDC-679", "classify", HoldMode.Session, SessionId: "s-1"));

        _liveness.Sessions.Remove("s-1");
        _book.Sweep();
        _liveness.Sessions.Add("s-1");

        Assert.That(_book.Check(stack.Token!).Status, Is.EqualTo(LockStatus.Granted));
    }

    [Test]
    public void ReleaseForSession_LetsGoOfThatSessionsHold()
    {
        _liveness.Sessions.Add("s-1");
        _book.Request(new LockRequest(LockNames.DevStack, "NDC-679", "AFE", HoldMode.Session, SessionId: "s-1"));

        Assert.That(_book.ReleaseForSession("s-1", "dev-stack").Status, Is.EqualTo(LockStatus.Ended));
        Assert.That(_book.Snapshot(), Is.Empty);
    }

    [Test]
    public void Reorder_MovesAWaiterAheadButNeverAheadOfTheHolder()
    {
        HoldWithProcess("Legacy job migration", "PersistenceTests.Jobs", 20036);
        var first = Ask("NDC-1190", "Api.Contacts.Tests");
        var second = Ask("GDPR", "Api.Gdpr.Tests");

        Assert.That(_book.Reorder(second.Token!, 0), Is.True);

        Assert.That(_book.Check(second.Token!).Position, Is.EqualTo(1));
        Assert.That(_book.Check(first.Token!).Position, Is.EqualTo(2));
        Assert.That(_book.Snapshot().First(ticket => ticket.Lock == LockNames.Tests).Who, Is.EqualTo("Legacy job migration"));
    }

    [Test]
    public void Reorder_ToTheBack()
    {
        HoldWithProcess("Legacy job migration", "PersistenceTests.Jobs", 20036);
        var first = Ask("NDC-1190", "Api.Contacts.Tests");
        var second = Ask("GDPR", "Api.Gdpr.Tests");

        _book.Reorder(first.Token!, 99);

        Assert.That(_book.Check(first.Token!).Position, Is.EqualTo(2));
        Assert.That(_book.Check(second.Token!).Position, Is.EqualTo(1));
    }

    [Test]
    public void Reorder_RefusesTheHolder()
    {
        var holder = HoldWithProcess("Legacy job migration", "PersistenceTests.Jobs", 20036);
        Ask("NDC-1190", "Api.Contacts.Tests");

        Assert.That(_book.Reorder(holder, 1), Is.False);
    }

    [Test]
    public void Cancel_TakesAWaiterOutAndTellsItWhy()
    {
        HoldWithProcess("Legacy job migration", "PersistenceTests.Jobs", 20036);
        var waiter = Ask("NDC-1190", "Api.Contacts.Tests");

        Assert.That(_book.Cancel(waiter.Token!), Is.True);

        var answer = _book.Check(waiter.Token!);
        Assert.That(answer.Status, Is.EqualTo(LockStatus.Ended));
        Assert.That(answer.Detail, Does.Contain("Nigel"));
    }

    [Test]
    public void Cancel_NeverTouchesAHolder()
    {
        var holder = HoldWithProcess("Legacy job migration", "PersistenceTests.Jobs", 20036);

        Assert.That(_book.Cancel(holder), Is.False);
        Assert.That(_book.Check(holder).Status, Is.EqualTo(LockStatus.Granted));
    }

    [Test]
    public void ForceRelease_TakesTheLockAndGrantsTheNext()
    {
        HoldWithProcess("Legacy job migration", "PersistenceTests.Jobs", 20036);
        var waiter = Ask("NDC-1190", "Api.Contacts.Tests");

        Assert.That(_book.ForceRelease(LockNames.Tests), Is.True);
        Assert.That(_book.Check(waiter.Token!).Status, Is.EqualTo(LockStatus.Granted));
    }

    [Test]
    public void ARestartResumesHoldersAndQueueExactly()
    {
        var holder = HoldWithProcess("Legacy job migration", "PersistenceTests.Jobs", 20036);
        var waiter = Ask("NDC-1190", "Api.Contacts.Tests");

        _clock.Advance(TimeSpan.FromMinutes(10));
        var reopened = Open();

        Assert.That(reopened.Check(holder).Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(reopened.Check(waiter.Token!).Status, Is.EqualTo(LockStatus.Waiting), "a waiter gets fresh grace after a restart");
    }

    [Test]
    public void ARestartGivesAnUnattachedGrantAFreshReservation()
    {
        var holder = Ask("Legacy job migration", "PersistenceTests.Jobs");

        _clock.Advance(TimeSpan.FromMinutes(30));
        var reopened = Open();

        Assert.That(reopened.Check(holder.Token!).Status, Is.EqualTo(LockStatus.Granted));
    }

    [Test]
    public void History_RecordsWaitingAndHolding()
    {
        var holder = HoldWithProcess("Legacy job migration", "PersistenceTests.Jobs", 20036);
        var waiter = Ask("NDC-1190", "Api.Contacts.Tests");

        _clock.Advance(TimeSpan.FromMinutes(1));
        _book.Check(waiter.Token!);
        _clock.Advance(TimeSpan.FromMinutes(1));
        _book.Check(waiter.Token!);
        _book.Release(holder);

        var history = new LockHistory(Path.Combine(_directory, "locks-history.jsonl")).Read();
        var released = history.Single(lockEvent => lockEvent.Kind == LockEventKind.Released);
        var granted = history.Last(lockEvent => lockEvent.Kind == LockEventKind.Granted);

        Assert.That(released.HeldSeconds, Is.EqualTo(120));
        Assert.That(granted.Who, Is.EqualTo("NDC-1190"));
        Assert.That(granted.WaitedSeconds, Is.EqualTo(120));
    }

    [Test]
    public void Changed_IsRaisedForAChangeButNotForAPlainCheck()
    {
        var holder = HoldWithProcess("Legacy job migration", "PersistenceTests.Jobs", 20036);
        var raised = 0;
        _book.Changed += (_, _) => raised++;

        _book.Check(holder);
        Assert.That(raised, Is.Zero);

        _book.Release(holder);
        Assert.That(raised, Is.EqualTo(1));
    }

    private LockAnswer Ask(string who, string purpose) =>
        _book.Request(new LockRequest(LockNames.Tests, who, purpose, HoldMode.Process, @"C:\dev\tfsgit"));

    private string HoldWithProcess(string who, string purpose, int processId)
    {
        var answer = Ask(who, purpose);
        _liveness.Processes[processId] = $"start-{processId}";
        _book.Attach(answer.Token!, processId);

        return answer.Token!;
    }

    private LockBook Open() =>
        new(new LockStateStore(Path.Combine(_directory, "locks.json")),
            new LockHistory(Path.Combine(_directory, "locks-history.jsonl")),
            _liveness,
            _clock);

    private sealed class MovableClock(DateTimeOffset start) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = start;

        public void Advance(TimeSpan span) => UtcNow += span;
    }

    private sealed class FakeLiveness : ILockLiveness
    {
        public Dictionary<int, string> Processes { get; } = [];

        public HashSet<string> Sessions { get; } = [];

        public string? StartOf(int processId) => Processes.GetValueOrDefault(processId);

        public bool IsSessionAlive(string sessionId) => Sessions.Contains(sessionId);
    }
}
