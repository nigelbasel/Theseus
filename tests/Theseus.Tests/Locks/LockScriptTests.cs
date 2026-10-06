using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using Theseus.App.Services;
using Theseus.Core.Abstractions;
using Theseus.Core.Attention;
using Theseus.Core.Locks;
using Theseus.Core.Mcp;
using Theseus.Core.Sessions;

namespace Theseus.Tests.Locks;

/// <summary>
/// The client script under Git Bash against a real Theseus lock server on a free port: what sessions
/// will actually run.
/// </summary>
[TestFixture]
public class LockScriptTests
{
    private const string GitBash = @"C:\Program Files\Git\bin\bash.exe";

    private string _directory = string.Empty;
    private McpHttpServer _server = null!;
    private LockBook _book = null!;
    private string _url = string.Empty;

    [SetUp]
    public void SetUp()
    {
        if (!File.Exists(GitBash))
        {
            Assert.Ignore("Git Bash is not installed here.");
        }

        _directory = Path.Combine(Path.GetTempPath(), $"theseus-lock-script-{Guid.NewGuid():N}");
        _book = new LockBook(
            new LockStateStore(Path.Combine(_directory, "locks.json")),
            new LockHistory(Path.Combine(_directory, "locks-history.jsonl")),
            new RegistryLockLiveness(new ProcessProbe(), new NoSessions(), new SystemClock()),
            new SystemClock());

        var port = FreePort();
        var handler = new McpHandler(new WorkLog(Path.Combine(_directory, "work.jsonl")), () => [], _ => null, () => DateTimeOffset.UtcNow);

        _server = new McpHttpServer(handler, port, new LockHttpApi(_book, () => DateTimeOffset.UtcNow));
        Assert.That(_server.Start(), Is.True);
        _url = $"http://localhost:{port}/locks";
    }

    [TearDown]
    public async Task TearDown()
    {
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void Run_TakesTheLockRunsTheCommandAndReleasesIt()
    {
        var (exitCode, output) = Script("run \"Legacy job migration\" \"PersistenceTests.Jobs\" -- bash -c 'exit 7'");

        Assert.That(exitCode, Is.EqualTo(7), output);
        Assert.That(output, Does.Contain("LOCK HELD"));
        Assert.That(_book.Snapshot(), Is.Empty);

        var kinds = new LockHistory(Path.Combine(_directory, "locks-history.jsonl")).Read().Select(lockEvent => lockEvent.Kind);
        Assert.That(kinds, Is.EqualTo(new[] { LockEventKind.Requested, LockEventKind.Granted, LockEventKind.Attached, LockEventKind.Released }));
    }

    [Test]
    public void Run_RefusesAtOnceWhenBusy_AndLeavesNoTicketBehind()
    {
        HoldTestsLock();

        var (exitCode, output) = Script("run \"NDC-1190\" \"Api.Contacts.Tests\" -- bash -c 'exit 0'");

        Assert.That(exitCode, Is.EqualTo(1), output);
        Assert.That(output, Does.Contain("LOCK BUSY"));
        Assert.That(output, Does.Contain("Legacy job migration"));
        Assert.That(_book.Snapshot(), Has.Count.EqualTo(1));
    }

    [Test]
    public void WaitThenRunWithToken_RunsUnderTheGrantedLock()
    {
        var (waitCode, waitOutput) = Script("wait \"NDC-1190\" \"Api.Contacts.Tests\" --wait 30");

        Assert.That(waitCode, Is.Zero, waitOutput);

        var token = waitOutput.Split("Token: ")[1].Split('\n')[0].Trim();
        var (runCode, runOutput) = Script($"run --token {token} -- bash -c 'exit 0'");

        Assert.That(runCode, Is.Zero, runOutput);
        Assert.That(_book.Snapshot(), Is.Empty);
    }

    [Test]
    public void Status_ShowsTheHolder()
    {
        HoldTestsLock();

        var (_, output) = Script("status");

        Assert.That(output, Does.Contain("tests: HELD by Legacy job migration for PersistenceTests.Jobs"));
        Assert.That(output, Does.Contain("dev-stack: free"));
    }

    [Test]
    public void TheseusNotRunning_RefusesWithoutWaiting()
    {
        _url = "http://localhost:1/locks";

        var (exitCode, output) = Script("run \"NDC-1190\" \"Api.Contacts.Tests\" -- bash -c 'exit 0'");

        Assert.That(exitCode, Is.EqualTo(1), output);
        Assert.That(output, Does.Contain("THESEUS NOT REACHABLE"));
    }

    [Test]
    public void AnOldStyleLockWhoseHolderHasGone_IsClearedAndDoesNotBlock()
    {
        Directory.CreateDirectory(LegacyLockDirectory);
        File.WriteAllText(Path.Combine(LegacyLockDirectory, "holder"), "who:     Old run\npid:     999999\nmode:    run\n");

        var (exitCode, output) = Script("run \"NDC-1190\" \"Api.Contacts.Tests\" -- bash -c 'exit 0'");

        Assert.That(exitCode, Is.Zero, output);
        Assert.That(Directory.Exists(LegacyLockDirectory), Is.False);
    }

    [Test]
    public void Acquire_IsGone()
    {
        var (exitCode, output) = Script("acquire \"Someone\" \"Something\"");

        Assert.That(exitCode, Is.EqualTo(2));
        Assert.That(output, Does.Contain("lock_acquire"));
    }

    private string LegacyLockDirectory => Path.Combine(_directory, "ndc-test.lock");

    private void HoldTestsLock()
    {
        var answer = _book.Request(new LockRequest(LockNames.Tests, "Legacy job migration", "PersistenceTests.Jobs", HoldMode.Process));
        _book.Attach(answer.Token!, Environment.ProcessId);
    }

    private (int ExitCode, string Output) Script(string arguments)
    {
        var script = Path.Combine(RepositoryRoot(), "tools", "locks", "ndc-test-lock.sh").Replace('\\', '/');
        var start = new ProcessStartInfo(GitBash)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _directory.Length > 0 && Directory.Exists(_directory) ? _directory : Path.GetTempPath(),
        };

        start.ArgumentList.Add("-c");
        start.ArgumentList.Add($"\"{script}\" {arguments}");
        start.Environment["THESEUS_LOCKS_URL"] = _url;
        start.Environment["NDC_TEST_LOCK_DIR"] = LegacyLockDirectory.Replace('\\', '/');

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();

        Assert.That(process.WaitForExit(TimeSpan.FromSeconds(60)), Is.True, "the script did not finish");

        return (process.ExitCode, output.Result + error.Result);
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }

    private sealed class NoSessions : ISessionRegistryReader
    {
        public SessionRegistrySnapshot Read() => SessionRegistrySnapshot.Empty;
    }
}
