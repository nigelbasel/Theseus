using Theseus.Core.Abstractions;
using Theseus.Core.Locks;

namespace Theseus.Tests.Locks.Scopes;

[TestFixture]
public class LeastScopeAndDuplicateTests
{
    private const string Tree = @"E:\work\nuf-1\Fabrikam";
    private const string Build = "dotnet build Fabrikam.sln --no-restore";
    private const string Suite = "dotnet test Fabrikam.PersistenceTests --no-build";

    private string _directory = string.Empty;
    private LockBook _book = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-least-{Guid.NewGuid():N}");
        _book = new LockBook(
            new LockStateStore(Path.Combine(_directory, "locks.json")),
            new LockHistory(Path.Combine(_directory, "locks-history.jsonl")),
            new LeastLiveness(),
            new LeastClock(),
            table: ResourceTable.Seeded(),
            cardFamily: card => card.EndsWith("-verifier", StringComparison.Ordinal) ? card[..^"-verifier".Length] : card);
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
    public void ACommandItRecognises_IsHeldForItsLeastScope()
    {
        LockAnswer build = Run("nuf-1", Build);

        Assert.That(Scope(build), Is.EqualTo(new[] { "cpu-build", "worktree:e:/work/nuf-1/fabrikam" }));
    }

    [Test]
    public void DeclaringLessThanTheCommandNeeds_StillHoldsWhatItNeeds()
    {
        LockAnswer suite = _book.Request(new LockRequest(string.Empty, "nuf-1", "suite", HoldMode.Process, Tree,
            Scope: [new ResourceClaim("port:8080", 1)], Card: "nuf-1", Command: Suite, Commit: "abc"));

        Assert.That(Scope(suite), Is.EqualTo(new[] { "port:8080", "sql-containers", "worktree:e:/work/nuf-1/fabrikam" }));
    }

    [Test]
    public void ACommandItDoesNotRecognise_WithNoScope_TakesTheOldTestsScope()
    {
        LockAnswer other = Run("nuf-1", "bash run-everything.sh");

        Assert.That(Scope(other), Is.EqualTo(new[] { "cpu-build", "docker-build", "sql-containers" }));
    }

    [Test]
    public void TheSameRunAskedForTwice_IsRefused_SayingWhoIsRunningIt()
    {
        Run("nuf-1", Suite, "first");

        LockAnswer again = Run("nuf-1", Suite + "  ", "second");

        Assert.That(again.Token, Is.Null, "the running ticket's token is its holder's secret");
        Assert.That(again.Status, Is.EqualTo(LockStatus.Refused));
        Assert.That(again.Detail, Does.StartWith("nuf-1 is already running this command on abc (granted, since"));
        Assert.That(again.Detail, Does.Contain("ndc-test-lock.sh status"));
    }

    [Test]
    public void AVerifierOfTheSameCard_RunningTheSameCommandOnTheSameCommit_IsTheSameRun()
    {
        Run("nuf-1", Suite);

        Assert.That(Run("nuf-1-verifier", Suite, "verify").Status, Is.EqualTo(LockStatus.Refused));
        Assert.That(Run("nuf-2", Suite, "other card").Status, Is.EqualTo(LockStatus.Waiting));
    }

    [Test]
    public void AskingAgainOnPurpose_QueuesASecondRun()
    {
        Run("nuf-1", Suite, "first");

        LockAnswer again = _book.Request(new LockRequest(string.Empty, "nuf-1", "flaky rerun", HoldMode.Process, Tree,
            Card: "nuf-1", Command: Suite, Commit: "abc", Again: true));

        Assert.That(again.Status, Is.EqualTo(LockStatus.Waiting));
    }

    [Test]
    public void AFinishedRun_IsRecordedWithItsExitCode_AndDoesNotRefuseTheNext()
    {
        LockAnswer first = Run("nuf-1", Suite, "first");

        _book.Finish(first.Token!, 3);

        Assert.That(_book.RecentRuns().Single().ExitCode, Is.EqualTo(3));
        Assert.That(Run("nuf-1", Suite, "second").Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(File.ReadAllText(Path.Combine(_directory, "locks-history.jsonl")), Does.Contain("\"exitCode\":3").And.Contain("\"commit\":\"abc\""));
    }

    private LockAnswer Run(string card, string command, string purpose = "run") =>
        _book.Request(new LockRequest(string.Empty, card, purpose, HoldMode.Process, Tree, Card: card, Command: command, Commit: "abc"));

    private string[] Scope(LockAnswer answer) =>
        [.. _book.Snapshot().Single(ticket => ticket.Token == answer.Token).Claims.Select(claim => claim.Resource)];

    private sealed class LeastClock : ISystemClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
    }

    private sealed class LeastLiveness : ILockLiveness
    {
        public string? StartOf(int processId) => null;

        public bool IsSessionAlive(string sessionId) => true;
    }
}
