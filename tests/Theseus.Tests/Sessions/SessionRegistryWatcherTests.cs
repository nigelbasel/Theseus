using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class SessionRegistryWatcherTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private string _directory = string.Empty;
    private CountingReader _reader = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-watch-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _reader = new CountingReader();
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void Changed_FiresWhenASessionRegisters()
    {
        using var watcher = Watcher();
        var fired = new ManualResetEventSlim();
        watcher.Changed += (_, _) => fired.Set();

        Write("4242.json");

        Assert.That(fired.Wait(Patience), Is.True);
    }

    [Test]
    public void Changed_FiresOnceForAFileWrittenSeveralTimesInQuickSuccession()
    {
        using var watcher = Watcher();
        var count = 0;
        watcher.Changed += (_, _) => Interlocked.Increment(ref count);

        Write("4242.json");
        Write("4242.json");
        Write("4242.json");

        Assert.That(WaitFor(() => Volatile.Read(ref count) >= 1), Is.True);
        Thread.Sleep(400);

        Assert.That(Volatile.Read(ref count), Is.EqualTo(1),
            "the registry file is written more than once as a session starts");
    }

    [Test]
    public void Changed_FiresWhenASessionEndsAndItsEntryIsRemoved()
    {
        Write("4242.json");

        using var watcher = Watcher();
        var fired = new ManualResetEventSlim();
        watcher.Changed += (_, _) => fired.Set();

        File.Delete(Path.Combine(_directory, "4242.json"));

        Assert.That(fired.Wait(Patience), Is.True);
    }

    [Test]
    public void Changed_CarriesAFreshSnapshotRatherThanJustANotification()
    {
        using var watcher = Watcher();
        SessionRegistrySnapshot? received = null;
        var fired = new ManualResetEventSlim();
        watcher.Changed += (_, snapshot) =>
        {
            received = snapshot;
            fired.Set();
        };

        Write("4242.json");

        Assert.That(fired.Wait(Patience), Is.True);
        Assert.That(received, Is.Not.Null);
        Assert.That(_reader.Reads, Is.GreaterThan(0));
    }

    [Test]
    public void Watcher_ReportsThatItIsNotWatchingWhenTheRegistryDirectoryDoesNotExist()
    {
        using var watcher = new SessionRegistryWatcher(
            Path.Combine(_directory, "no-such-directory"), _reader);

        Assert.That(watcher.IsWatching, Is.False);
    }

    [Test]
    public void ReadNow_ReadsWithoutWaitingForAChange()
    {
        using var watcher = Watcher();

        Assert.That(watcher.ReadNow(), Is.Not.Null);
        Assert.That(_reader.Reads, Is.EqualTo(1));
    }

    [Test]
    public void Dispose_StopsFurtherNotifications()
    {
        var watcher = Watcher();
        var count = 0;
        watcher.Changed += (_, _) => Interlocked.Increment(ref count);
        watcher.Dispose();

        Write("4242.json");
        Thread.Sleep(500);

        Assert.That(Volatile.Read(ref count), Is.Zero);
    }

    private SessionRegistryWatcher Watcher() =>
        new(_directory, _reader, TimeSpan.FromMilliseconds(120));

    private void Write(string name) =>
        File.WriteAllText(Path.Combine(_directory, name), """{"pid":4242}""");

    private static bool WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return condition();
    }

    private sealed class CountingReader : ISessionRegistryReader
    {
        private int _reads;

        public int Reads => Volatile.Read(ref _reads);

        public SessionRegistrySnapshot Read()
        {
            Interlocked.Increment(ref _reads);

            return SessionRegistrySnapshot.Empty;
        }
    }
}
