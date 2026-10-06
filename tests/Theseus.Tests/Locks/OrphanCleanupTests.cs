using Theseus.Core.Abstractions;
using Theseus.Core.Locks;
using Theseus.Core.Sessions;

namespace Theseus.Tests.Locks;

[TestFixture]
public class OrphanCleanupTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    [TestCase("testhost", null, true)]
    [TestCase("testhost.x86", null, true)]
    [TestCase("vstest.console", null, true)]
    [TestCase("dotnet", @"""C:\Program Files\dotnet\dotnet.exe"" exec --runtimeconfig x.json C:\nuget\microsoft.testplatform\testhost.dll --port 5000", true)]
    [TestCase("dotnet", @"dotnet C:\Program Files\dotnet\sdk\10.0.100\MSBuild.dll /nodemode:1 /nodeReuse:true", false)]
    [TestCase("dotnet", @"dotnet C:\Program Files\dotnet\sdk\10.0.100\Roslyn\bincore\VBCSCompiler.dll -pipename:abc", false)]
    [TestCase("VBCSCompiler", null, false)]
    [TestCase("MSBuild", null, false)]
    [TestCase("dotnet", null, false)]
    public void OnlyTestHostsAreRecognised(string name, string? commandLine, bool expected)
    {
        Assert.That(TestHostRecogniser.IsTestHost(name, commandLine), Is.EqualTo(expected));
    }

    [Test]
    public void TheRunningTestProcessIsFoundUnderItsParent()
    {
        var parent = ProcessSnapshot.Capture().Single(entry => entry.ProcessId == Environment.ProcessId).ParentProcessId;

        var hosts = new WindowsProcessTree(new ProcessProbe()).TestHostsUnder(parent);

        Assert.That(hosts.Select(host => host.ProcessId), Does.Contain(Environment.ProcessId));
    }

    [Test]
    public void ParseDockerCreated_ReadsDockersOwnFormat()
    {
        Assert.That(OrphanCleaner.ParseDockerCreated("2026-10-06 11:21:51 +0200 CEST"),
            Is.EqualTo(new DateTimeOffset(2026, 10, 6, 9, 21, 51, TimeSpan.Zero)));
        Assert.That(OrphanCleaner.ParseDockerCreated("not a date"), Is.Null);
    }

    [Test]
    public void Clean_EndsOnlyHostsThatAreStillTheSameProcess_AndContainersInsideTheHold()
    {
        var killed = new List<int>();
        var removed = new List<string>();
        var cleaner = new OrphanCleaner(
            pid => pid switch { 1 => "s1", 2 => "reused", _ => null },
            killed.Add,
            () => [("before", Nine.AddMinutes(-1)), ("during", Nine.AddMinutes(5)), ("after", Nine.AddMinutes(61))],
            removed.Add);

        var detail = cleaner.Clean(
            [new HeldProcess(1, "s1", "testhost"), new HeldProcess(2, "s2", "testhost"), new HeldProcess(3, "s3", "testhost")],
            Nine,
            Nine.AddHours(1));

        Assert.That(killed, Is.EqualTo(new[] { 1 }));
        Assert.That(removed, Is.EqualTo(new[] { "during" }));
        Assert.That(detail, Does.Contain("1 test host(s)").And.Contain("1 container(s)"));
    }

    [Test]
    public void Clean_WithNothingLeft_SaysNothing()
    {
        var cleaner = new OrphanCleaner(_ => null, _ => Assert.Fail("nothing should be killed"), () => [], _ => Assert.Fail("nothing should be removed"));

        Assert.That(cleaner.Clean([new HeldProcess(1, "s1", "testhost")], Nine, Nine.AddHours(1)), Is.Null);
    }

    [Test]
    public void ADeadTestRunIsClearedUpBeforeTheNextIsGranted()
    {
        using var fixture = new BookFixture();
        var holder = fixture.HoldTests(4242);
        var waiter = fixture.Book.Request(new LockRequest(LockNames.Tests, "NDC-1190", "Api.Contacts.Tests", HoldMode.Process));

        fixture.Tree.Hosts[4242] = [new HeldProcess(5000, "t", "testhost")];
        fixture.Book.Sweep();
        fixture.Liveness.Processes.Remove(4242);

        Assert.That(fixture.Book.Check(waiter.Token!).Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(fixture.Cleaner.Calls, Has.Count.EqualTo(1));
        Assert.That(fixture.Cleaner.Calls[0].Hosts.Single().ProcessId, Is.EqualTo(5000));
        Assert.That(fixture.Cleaner.GrantedWhenCleaned, Is.False, "the waiter must not hold the lock while the clean-up runs");
        Assert.That(holder, Is.Not.Empty);
    }

    [Test]
    public void ANormalReleaseCleansNothing()
    {
        using var fixture = new BookFixture();
        var holder = fixture.HoldTests(4242);
        fixture.Tree.Hosts[4242] = [new HeldProcess(5000, "t", "testhost")];
        fixture.Book.Sweep();

        fixture.Book.Release(holder);

        Assert.That(fixture.Cleaner.Calls, Is.Empty);
    }

    [Test]
    public void OtherLocksAreNeverCleanedUp()
    {
        using var fixture = new BookFixture();
        var answer = fixture.Book.Request(new LockRequest(LockNames.Llama, "Someone", "classify", HoldMode.Process));
        fixture.Liveness.Processes[77] = "s";
        fixture.Book.Attach(answer.Token!, 77);
        fixture.Tree.Hosts[77] = [new HeldProcess(5000, "t", "testhost")];
        fixture.Book.Sweep();

        fixture.Liveness.Processes.Remove(77);
        fixture.Book.Sweep();

        Assert.That(fixture.Cleaner.Calls, Is.Empty);
    }

    [Test]
    public void RecordedHostsSurviveARestart()
    {
        using var fixture = new BookFixture();
        fixture.HoldTests(4242);
        fixture.Tree.Hosts[4242] = [new HeldProcess(5000, "t", "testhost")];
        fixture.Book.Sweep();

        var reopened = fixture.Open();
        fixture.Tree.Hosts.Clear();
        fixture.Liveness.Processes.Remove(4242);
        reopened.Sweep();

        Assert.That(fixture.Cleaner.Calls.Single().Hosts.Single().ProcessId, Is.EqualTo(5000));
    }

    private sealed class BookFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"theseus-orphans-{Guid.NewGuid():N}");

        public BookFixture()
        {
            Book = Open();
            Cleaner.Book = Book;
        }

        public LockBook Book { get; }

        public FakeLiveness Liveness { get; } = new();

        public FakeTree Tree { get; } = new();

        public FakeCleaner Cleaner { get; } = new();

        public LockBook Open() =>
            new(new LockStateStore(Path.Combine(_directory, "locks.json")),
                new LockHistory(Path.Combine(_directory, "locks-history.jsonl")),
                Liveness,
                new FixedClock(Nine),
                Tree,
                Cleaner);

        public string HoldTests(int processId)
        {
            var answer = Book.Request(new LockRequest(LockNames.Tests, "Legacy job migration", "PersistenceTests.Jobs", HoldMode.Process));
            Liveness.Processes[processId] = $"start-{processId}";
            Book.Attach(answer.Token!, processId);

            return answer.Token!;
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class FakeLiveness : ILockLiveness
    {
        public Dictionary<int, string> Processes { get; } = [];

        public string? StartOf(int processId) => Processes.GetValueOrDefault(processId);

        public bool IsSessionAlive(string sessionId) => true;
    }

    private sealed class FakeTree : IProcessTree
    {
        public Dictionary<int, IReadOnlyList<HeldProcess>> Hosts { get; } = [];

        public IReadOnlyList<HeldProcess> TestHostsUnder(int processId) => Hosts.GetValueOrDefault(processId) ?? [];
    }

    private sealed class FakeCleaner : IOrphanCleaner
    {
        public LockBook? Book { get; set; }

        public List<(IReadOnlyList<HeldProcess> Hosts, DateTimeOffset From, DateTimeOffset To)> Calls { get; } = [];

        public bool GrantedWhenCleaned { get; private set; }

        public string? Clean(IReadOnlyList<HeldProcess> testHosts, DateTimeOffset fromUtc, DateTimeOffset toUtc)
        {
            Calls.Add((testHosts, fromUtc, toUtc));
            GrantedWhenCleaned = Book is not null && Book.Snapshot().Any(ticket => ticket.IsHolder && ticket.Who == "NDC-1190");

            return "Cleaned up after it: 1 test host(s).";
        }
    }
}
