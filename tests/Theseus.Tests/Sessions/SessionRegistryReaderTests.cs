using System.Text.Json;
using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class SessionRegistryReaderTests
{
    private string _directory = null!;
    private FakeProcessProbe _probe = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "orchestrator-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _probe = new FakeProcessProbe();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private SessionRegistryReader Reader() => new(_directory, _probe);

    private void WriteEntry(int pid, string sessionId, string procStart, string entrypoint = "cli",
        string status = "idle", string? name = null, long updatedAt = 0, long statusUpdatedAt = 0)
    {
        var entry = new
        {
            pid,
            sessionId,
            cwd = @"C:\work\repo",
            startedAt = 1787689133099L,
            procStart,
            version = "2.1.245",
            kind = "interactive",
            entrypoint,
            name = name ?? $"session-{pid}",
            status,
            updatedAt,
            statusUpdatedAt,
        };

        File.WriteAllText(Path.Combine(_directory, $"{pid}.json"), JsonSerializer.Serialize(entry));
    }

    [Test]
    public void Read_WhenTheDirectoryIsAbsent_ReturnsAnEmptySnapshot()
    {
        Directory.Delete(_directory, recursive: true);

        var snapshot = Reader().Read();

        Assert.That(snapshot.Live, Is.Empty);
        Assert.That(snapshot.CorpseCount, Is.Zero);
    }

    [Test]
    public void Read_ReturnsAnEntryWhoseProcessIsStillRunning()
    {
        WriteEntry(40000, "6d6942b6-162a-4da4-a420-b265967ed98a", "134321627304177336");
        _probe.Alive[40000] = "134321627304177336";

        var snapshot = Reader().Read();

        Assert.That(snapshot.Live, Has.Count.EqualTo(1));
        Assert.That(snapshot.Live[0].Pid, Is.EqualTo(40000));
        Assert.That(snapshot.Live[0].SessionId, Is.EqualTo("6d6942b6-162a-4da4-a420-b265967ed98a"));
        Assert.That(snapshot.CorpseCount, Is.Zero);
    }

    [Test]
    public void Read_CountsAnEntryWhoseProcessHasGoneAsACorpse()
    {
        WriteEntry(40000, "abc", "134321627304177336");

        var snapshot = Reader().Read();

        Assert.That(snapshot.Live, Is.Empty);
        Assert.That(snapshot.CorpseCount, Is.EqualTo(1));
    }

    [Test]
    public void Read_CountsAReusedProcessIdAsACorpse()
    {
        WriteEntry(40000, "abc", "134321627304177336");
        _probe.Alive[40000] = "999999999999999999";

        var snapshot = Reader().Read();

        Assert.That(snapshot.Live, Is.Empty);
        Assert.That(snapshot.CorpseCount, Is.EqualTo(1));
    }

    [Test]
    public void Read_NeverDeletesRegistryFiles()
    {
        WriteEntry(40000, "abc", "134321627304177336");

        Reader().Read();

        Assert.That(File.Exists(Path.Combine(_directory, "40000.json")), Is.True);
    }

    [Test]
    public void Read_SkipsMalformedFilesAndCountsThem()
    {
        File.WriteAllText(Path.Combine(_directory, "bad.json"), "{ this is not json");
        WriteEntry(40000, "abc", "1");
        _probe.Alive[40000] = "1";

        var snapshot = Reader().Read();

        Assert.That(snapshot.Live, Has.Count.EqualTo(1));
        Assert.That(snapshot.UnreadableCount, Is.EqualTo(1));
    }

    [Test]
    public void Read_IgnoresTheAdjacentKeyFiles()
    {
        File.WriteAllText(Path.Combine(_directory, "40000.abc123.key"), "{\"peerToken\":\"x\"}");
        WriteEntry(40000, "abc", "1");
        _probe.Alive[40000] = "1";

        var snapshot = Reader().Read();

        Assert.That(snapshot.Live, Has.Count.EqualTo(1));
        Assert.That(snapshot.UnreadableCount, Is.Zero);
    }

    [Test]
    public void Read_SurfacesSeveralLiveEntriesSharingOneSessionId()
    {
        WriteEntry(1, "shared-id", "11");
        WriteEntry(2, "shared-id", "22");
        _probe.Alive[1] = "11";
        _probe.Alive[2] = "22";

        var snapshot = Reader().Read();

        Assert.That(snapshot.Live, Has.Count.EqualTo(2));
        Assert.That(snapshot.WithSessionId("shared-id"), Has.Count.EqualTo(2));
    }

    [Test]
    public void IsAdoptable_IsTrueOnlyForCommandLineSessions()
    {
        WriteEntry(1, "a", "11", entrypoint: "cli");
        WriteEntry(2, "b", "22", entrypoint: "sdk-cli");
        _probe.Alive[1] = "11";
        _probe.Alive[2] = "22";

        var snapshot = Reader().Read();

        Assert.That(snapshot.Live.Single(s => s.Pid == 1).IsAdoptable, Is.True);
        Assert.That(snapshot.Live.Single(s => s.Pid == 2).IsAdoptable, Is.False);
    }

    private sealed class FakeProcessProbe : IProcessProbe
    {
        public Dictionary<int, string> Alive { get; } = [];

        public string? GetStartFileTime(int pid) => Alive.TryGetValue(pid, out var value) ? value : null;
    }

    [Test]
    public void Read_KeepsTheMomentTheEntryWasLastWritten()
    {
        WriteEntry(40000, "abc", "134321627304177336", updatedAt: 1787778152116L);
        _probe.Alive[40000] = "134321627304177336";

        Assert.That(Reader().Read().Live.Single().LastSeenUtc,
            Is.EqualTo(DateTimeOffset.FromUnixTimeMilliseconds(1787778152116L)),
            "the last moment a session was known to be alive is the only evidence about one that was killed");
    }

    [Test]
    public void Read_KeepsTheMomentTheStatusLastChanged()
    {
        WriteEntry(40000, "abc", "134321627304177336", statusUpdatedAt: 1787778152116L);
        _probe.Alive[40000] = "134321627304177336";

        Assert.That(Reader().Read().Live.Single().StatusSinceUtc,
            Is.EqualTo(DateTimeOffset.FromUnixTimeMilliseconds(1787778152116L)),
            "dated by the session that changed it, not by the debounced watcher that noticed");
    }

    [Test]
    public void Read_LeavesTheTimestampsUnsetWhenAnOlderBuildDidNotWriteThem()
    {
        WriteEntry(40000, "abc", "134321627304177336");
        _probe.Alive[40000] = "134321627304177336";

        var session = Reader().Read().Live.Single();

        Assert.That(session.LastSeenUtc, Is.Null, "absent must not read as 1970");
        Assert.That(session.StatusSinceUtc, Is.Null);
    }
}
