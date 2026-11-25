using Theseus.Core.Domain;
using Theseus.Core.Persistence;

namespace Theseus.Tests.Persistence;

/// <summary>Each run records the window it was launched into (project-view AC 20), and older lines still load.</summary>
[TestFixture]
public class RunWindowTests
{
    private string _directory = string.Empty;
    private string _path = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-run-window-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "runs.jsonl");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    private static SessionRun Run() =>
        new(Guid.NewGuid(), "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa", "a-card", @"C:\work\repo", null, SessionLaunchMode.FirstRun,
            "Carry on.", "A card", "#208DD7", new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero), null, 4242, 8080, null,
            SessionExitReason.Unknown);

    [Test]
    public void TheWindow_IsKept_AndSurvivesTheRunsEnd()
    {
        var log = new RunLog(_path);
        var run = Run() with { WindowName = "theseus-project-p" };
        log.Append(run);
        log.AppendEnd(new RunEnd(run.Id, new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.Zero), 0, SessionExitReason.ClosedCleanly));

        Assert.That(new RunLog(_path).Read().Single().WindowName, Is.EqualTo("theseus-project-p"));
    }

    [Test]
    public void ALineFromBeforeWindowsWereRecorded_StillLoads_WithNoWindow()
    {
        File.WriteAllText(_path,
            """{"id":"6f1a2b3c-4d5e-4f60-8a7b-9c0d1e2f3a4b","claudeSessionId":"s1","workstreamKey":"a-card","workingDirectory":"C:\\work","transcriptPath":null,"launchMode":"FirstRun","openingPrompt":"Carry on.","tabTitle":"A card","colorHex":"#208DD7","startedUtc":"2026-11-19T09:00:00+00:00","endedUtc":null,"shimProcessId":null,"claudeProcessId":null,"exitCode":null,"exitReason":"Unknown"}"""
            + Environment.NewLine);

        var run = new RunLog(_path).Read().Single();

        Assert.That((run.WorkstreamKey, run.WindowName), Is.EqualTo(("a-card", (string?)null)));
    }
}
