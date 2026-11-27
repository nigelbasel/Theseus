using Theseus.Core.Abstractions;
using Theseus.Core.Locks;

namespace Theseus.Tests.Locks;

/// <summary>
/// What Stop the project releases for a card once its session has ended (project-view-as-tabs AC 10): its session holds
/// and its places in every queue, and never a process hold, which ends with its run.
/// </summary>
[TestFixture]
public class CardLocksTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 26, 21, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private Liveness _liveness = null!;
    private LockBook _book = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-cardlocks-{Guid.NewGuid():N}");
        _liveness = new Liveness();
        _book = new LockBook(new LockStateStore(Path.Combine(_directory, "locks.json")),
            new LockHistory(Path.Combine(_directory, "locks-history.jsonl")), _liveness, new Clock());
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
    public void Release_EndsTheCardsSessionHold_AndPassesTheLockOn()
    {
        SessionHold("card-a", "session-a", LockNames.DevStack);
        SessionHold("card-b", "session-b", LockNames.DevStack);

        var released = CardLocks.Release(_book, "card-a", "session-a");

        Assert.That(released, Is.EqualTo(1));
        Assert.That(_book.Snapshot().Single(ticket => ticket.IsHolder).Card, Is.EqualTo("card-b"));
    }

    [Test]
    public void Release_TakesTheCardOutOfEveryQueue()
    {
        SessionHold("card-b", "session-b", LockNames.DevStack);
        SessionHold("card-a", "session-a", LockNames.DevStack);
        _book.Request(new LockRequest(LockNames.Tests, "B", "Suite", HoldMode.Process, @"C:\w\b", Card: "card-b"));
        _book.Request(new LockRequest(LockNames.Tests, "A", "Suite", HoldMode.Process, @"C:\w\a", Card: "card-a"));

        var released = CardLocks.Release(_book, "card-a", "session-a");

        Assert.That(released, Is.EqualTo(2));
        Assert.That(_book.Snapshot().Select(ticket => ticket.Card), Is.All.EqualTo("card-b"));
    }

    [Test]
    public void Release_NeverEndsAProcessHold_WhichEndsWithItsRun()
    {
        var answer = _book.Request(new LockRequest(LockNames.Tests, "A", "Suite", HoldMode.Process, @"C:\w\a", Card: "card-a"));
        _liveness.Processes[42] = "start-42";
        _book.Attach(answer.Token!, 42);

        var released = CardLocks.Release(_book, "card-a", "session-a");

        Assert.That(released, Is.Zero);
        Assert.That(_book.Snapshot().Single().IsHolder, Is.True);
    }

    [Test]
    public void Release_NeverEndsAHoldOrPlaceOfASessionStillAlive_WhateverItsCard()
    {
        SessionHold("card-a", "session-other", LockNames.DevStack);

        var released = CardLocks.Release(_book, "card-a", "session-a", isAlive: session => session == "session-other");

        Assert.That(released, Is.Zero);
        Assert.That(_book.Snapshot().Single().IsHolder, Is.True);
    }

    [Test]
    public void Of_FindsTheCardsTickets_ByCardOrByItsSession()
    {
        _liveness.Sessions.Add("session-a");
        _book.Request(new LockRequest(LockNames.DevStack, "A", "Stack", HoldMode.Session, SessionId: "session-a"));
        SessionHold("card-b", "session-b", LockNames.Llama);

        var tickets = CardLocks.Of(_book.Snapshot(), "card-a", "session-a");

        Assert.That(tickets.Select(ticket => ticket.Lock), Is.EqualTo(new[] { LockNames.DevStack }));
    }

    [Test]
    public void Releasable_IsASessionHoldOrAnyPlaceInAQueue_NeverAProcessHold()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CardLocks.Releasable(Ticket(HoldMode.Session, granted: true)), Is.True);
            Assert.That(CardLocks.Releasable(Ticket(HoldMode.Process, granted: false)), Is.True);
            Assert.That(CardLocks.Releasable(Ticket(HoldMode.Process, granted: true)), Is.False);
        });
    }

    private static LockTicket Ticket(HoldMode mode, bool granted) =>
        new("t", LockNames.Tests, "A", "Suite", mode, "key", Nine, Nine, GrantedUtc: granted ? Nine : null);

    private void SessionHold(string card, string session, string name)
    {
        _liveness.Sessions.Add(session);
        _book.Request(new LockRequest(name, card, "Work", HoldMode.Session, SessionId: session, Card: card));
    }

    private sealed class Clock : ISystemClock
    {
        public DateTimeOffset UtcNow => Nine;
    }

    private sealed class Liveness : ILockLiveness
    {
        public Dictionary<int, string> Processes { get; } = [];

        public HashSet<string> Sessions { get; } = [];

        public string? StartOf(int processId) => Processes.GetValueOrDefault(processId);

        public bool IsSessionAlive(string sessionId) => Sessions.Contains(sessionId);
    }
}
