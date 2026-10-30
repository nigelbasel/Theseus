using System.Text.Json;
using Theseus.Core.Status;
using Theseus.Core.Terminal;
using Theseus.Core.Launching;

namespace Theseus.Tests.Status;

/// <summary>
/// Sessions learn from a status file, read by their hook, that Theseus has stopped or restarted, since
/// Theseus can tell them nothing once it has gone (Nigel, 2026-10-30).
/// </summary>
[TestFixture]
public class TheseusStatusTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly Func<int, bool> Alive = _ => true;
    private static readonly Func<int, bool> Gone = _ => false;

    private string _directory = string.Empty;

    [SetUp]
    public void SetUp() => _directory = Directory.CreateTempSubdirectory("theseus-status-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void WhyNotRunning_TellsClosedFromCrashedFromNeverStarted()
    {
        var running = new TheseusStatus(42, Nine, null, "1.0");

        Assert.Multiple(() =>
        {
            Assert.That(TheseusStatusNotice.WhyNotRunning(running, Alive, Utc), Is.Null);
            Assert.That(TheseusStatusNotice.WhyNotRunning(running with { StoppedUtc = Nine.AddHours(8) }, Alive, Utc),
                Is.EqualTo("Theseus is not running: it was closed at 17:00"));
            Assert.That(TheseusStatusNotice.WhyNotRunning(running, Gone, Utc),
                Is.EqualTo("Theseus is not running: it stopped unexpectedly, having started at 09:00"));
            Assert.That(TheseusStatusNotice.WhyNotRunning(null, Alive, Utc), Is.EqualTo("Theseus is not running on this machine"));
        });
    }

    [Test]
    public void PreToolUse_DeniesATheseusToolWithTheReasonOnlyWhileTheseusIsDown()
    {
        var closed = new TheseusStatus(42, Nine, Nine.AddHours(8), "1.0");

        var refusal = StatusHook.PreToolUseReply("mcp__theseus__lock_acquire", closed, Alive, Utc);
        var reason = JsonDocument.Parse(refusal!).RootElement.GetProperty("hookSpecificOutput");

        Assert.Multiple(() =>
        {
            Assert.That(reason.GetProperty("permissionDecision").GetString(), Is.EqualTo("deny"));
            Assert.That(reason.GetProperty("permissionDecisionReason").GetString(), Does.StartWith("Theseus is not running: it was closed at 17:00."));
            Assert.That(StatusHook.PreToolUseReply("mcp__theseus__lock_acquire", closed with { StoppedUtc = null }, Alive, Utc), Is.Null);
            Assert.That(StatusHook.PreToolUseReply("mcp__atlassian__getJiraIssue", closed, Alive, Utc), Is.Null, "other servers' tools are not Theseus's to answer");
        });
    }

    [Test]
    public void PromptReply_TellsALaunchedSessionOfAChangeOnce()
    {
        var seen = new StatusSeenStore(_directory);
        var first = new TheseusStatus(42, Nine, null, "1.0");
        seen.Write("session-1", TheseusStatusNotice.Marker(first, Alive));

        Assert.That(StatusHook.PromptReply("session-1", seen, first, Alive, Utc), Is.Null, "nothing has changed");

        var stopped = first with { StoppedUtc = Nine.AddHours(1) };
        var told = StatusHook.PromptReply("session-1", seen, stopped, Alive, Utc);

        Assert.That(told, Does.Contain("it was closed at 10:00"));
        Assert.That(StatusHook.PromptReply("session-1", seen, stopped, Alive, Utc), Is.Null, "told once");

        var restarted = new TheseusStatus(43, Nine.AddHours(2), null, "1.0");

        Assert.That(StatusHook.PromptReply("session-1", seen, restarted, Alive, Utc), Does.Contain("Theseus restarted at 11:00"));
    }

    [Test]
    public void PromptReply_SaysNothingToASessionTheseusDidNotLaunch()
    {
        var seen = new StatusSeenStore(_directory);

        Assert.That(StatusHook.PromptReply("somebody-else", seen, new TheseusStatus(42, Nine, Nine, "1.0"), Alive, Utc), Is.Null);
    }

    [Test]
    public void TheStatusFile_RoundTrips()
    {
        var path = Path.Combine(_directory, "status.json");
        var status = new TheseusStatus(42, Nine, null, "2026.10.30.1");

        TheseusStatusFile.Write(status, path);

        Assert.That(TheseusStatusFile.Read(path), Is.EqualTo(status));
        Assert.That(TheseusStatusFile.Read(Path.Combine(_directory, "missing.json")), Is.Null);
    }

    [Test]
    public void ALaunchCarriesTheHookAsSettings_BeforeTheMcpConfiguration()
    {
        var settings = TheseusHookSettings.Json(@"C:\Program Files\Theseus\Theseus.Hook.exe");
        var spec = new LaunchSpec("session-1", LaunchMode.FirstRun, @"C:\claude.exe", @"C:\work", "Title", "Go.", [])
        {
            McpConfig = "{}",
            Settings = settings,
        };

        var argv = ClaudeArgumentBuilder.Build(spec).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(argv.IndexOf("--settings"), Is.LessThan(argv.IndexOf("--mcp-config")), "--mcp-config is variadic, so it goes last");
            Assert.That(argv[argv.IndexOf("--settings") + 1], Is.EqualTo(settings));
            Assert.That(JsonDocument.Parse(settings).RootElement.GetProperty("hooks").GetProperty("PreToolUse")[0]
                .GetProperty("matcher").GetString(), Is.EqualTo("mcp__theseus__.*"));
        });
    }
}
