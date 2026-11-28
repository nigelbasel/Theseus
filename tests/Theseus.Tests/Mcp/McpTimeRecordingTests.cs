using System.Text.Json.Nodes;
using Theseus.Core.Attention;
using Theseus.Core.Launching;
using Theseus.Core.Mcp;

namespace Theseus.Tests.Mcp;

/// <summary>
/// 7.3 in time_for: lock waits shown apart from machine time, attended time labelled unmeasured rather than 0, the method
/// break named; and sessions told, at launch and when they connect, that all time must be tracked.
/// </summary>
[TestFixture]
public class McpTimeRecordingTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private List<AttentionInterval> _intervals = [];
    private DateTimeOffset _now;
    private McpHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-mcp-time-{Guid.NewGuid():N}");
        _intervals = [];
        _now = Nine;
        _handler = new McpHandler(
            new WorkLog(Path.Combine(_directory, "work.jsonl")),
            () => _intervals,
            sessionId => sessionId == "session-1" ? new McpSession("session-1", "alpha", "Alpha") : null,
            () => _now,
            TimeZoneInfo.Utc);
        _handler.Handle("session-1", new JsonObject
        {
            ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = "start_work", ["arguments"] = new JsonObject { ["issue"] = "KITE-1" } },
        }.ToJsonString(), null);
        _now = Nine.AddHours(1);
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
    public void TimeFor_ShowsALockWaitApart_AndTakesItOutOfMachineTime()
    {
        _intervals = [new AttentionInterval(AttentionKind.Machine, "alpha", Nine, Nine.AddHours(1)) { Session = "session-1" }];
        _handler.LockWaits = () => [new LockWait("alpha", "session-1", "tests", Nine.AddMinutes(10), Nine.AddMinutes(25))];

        var report = TimeFor("2026-11-27T09:00:00Z");

        Assert.That((report["machineMinutes"]!.GetValue<int>(), report["lockWaitMinutes"]!.GetValue<int>()), Is.EqualTo((45, 15)));
    }

    [Test]
    public void TimeFor_ShowsAttendedTimeInTheseusApartFromAttendedTimeInATerminal()
    {
        _intervals =
        [
            new AttentionInterval(AttentionKind.Attended, "alpha", Nine, Nine.AddMinutes(20)) { Source = AttentionSampling.TheseusSource },
            new AttentionInterval(AttentionKind.Attended, "alpha", Nine.AddMinutes(20), Nine.AddMinutes(30)) { Source = AttentionSampling.TerminalSource },
        ];

        var report = TimeFor("2026-11-27T09:00:00Z");

        Assert.That((report["attendedInTheseusMinutes"]!.GetValue<int>(), report["attendedInTerminalMinutes"]!.GetValue<int>()),
            Is.EqualTo((20, 10)));
    }

    [Test]
    public void TimeFor_LabelsAttendedTimeUnmeasured_NeverZero_BeforeTwoCleanWeeks()
    {
        _handler.MethodBreaksRecorded = () => [new MethodBreak(MethodBreaks.AllTimeRecorded, Nine, "x")];

        var report = TimeFor("2026-11-27T09:00:00Z");

        Assert.That(report["attendedMinutes"], Is.Null);
        Assert.That(report["attended"]!.GetValue<string>(), Does.StartWith(MethodBreaks.UnmeasuredLabel));
        Assert.That(report["attendedNote"], Is.Not.Null);
    }

    [Test]
    public void TimeFor_NamesTheMethodBreak_WhenItsRangeSpansIt()
    {
        _handler.MethodBreaksRecorded = () => [new MethodBreak(MethodBreaks.AllTimeRecorded, Nine.AddMinutes(30), "machine time counts every session")];

        var report = TimeFor("2026-11-27T09:00:00Z");

        Assert.That(report["measurementChanged"]!.GetValue<string>(), Does.Contain("Measurement changed on 2026-11-27"));
    }

    [Test]
    public void Instructions_TellEverySessionThatAllTimeMustBeTracked_AndKeepTheDurationRule()
    {
        Assert.That(McpHandler.Instructions, Does.Contain(McpHandler.TrackAllTimeSentence));
        Assert.That(McpHandler.InstructionsFor(unattended: true), Does.Contain(McpHandler.TrackAllTimeSentence));
        Assert.That(McpHandler.Instructions, Does.Contain("Never estimate, report or log a duration of your own."));
    }

    [Test]
    public void TheLaunchText_TellsEverySessionThatAllTimeMustBeTracked()
    {
        Assert.That(WorkstreamIdentity.DefaultTemplate, Does.Contain("All time must be tracked").And.Contain("start_work").And.Contain("now_at"));
    }

    private JsonNode TimeFor(string from)
    {
        var response = _handler.Handle("session-1", new JsonObject
        {
            ["jsonrpc"] = "2.0", ["id"] = 2, ["method"] = "tools/call",
            ["params"] = new JsonObject { ["name"] = "time_for", ["arguments"] = new JsonObject { ["issue"] = "KITE-1", ["from"] = from } },
        }.ToJsonString(), null);

        return JsonNode.Parse(JsonNode.Parse(response.Body!)!["result"]!["content"]![0]!["text"]!.GetValue<string>())!;
    }
}
