using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class WorkflowActivityReaderTests
{
    private const string SessionId = "7d73cb6d-9784-4167-9235-8d84008e3500";

    private string _root = string.Empty;
    private string _runs = string.Empty;
    private WorkflowActivityReader _reader = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-journal-{Guid.NewGuid():N}");
        _runs = Path.Combine(_root, "C--dev-tfsgit", SessionId, "subagents", "workflows");

        Directory.CreateDirectory(_runs);
        _reader = new WorkflowActivityReader(_root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Read_CountsWhatHasBeenDispatchedAgainstWhatHasComeBack()
    {
        Journal("wf_8729a217-6ed", started: 33, finished: 12);

        var activity = _reader.Read(SessionId)!;

        Assert.That(activity.RunId, Is.EqualTo("wf_8729a217-6ed"));
        Assert.That(activity.Started, Is.EqualTo(33));
        Assert.That(activity.Finished, Is.EqualTo(12));
        Assert.That(activity.Describe, Is.EqualTo("workflow · 12/33 agents"));
        Assert.That(activity.InFlight, Is.True);
    }

    [Test]
    public void Read_KnowsWhenEverythingDispatchedHasComeBack()
    {
        Journal("wf_a", started: 5, finished: 5);

        Assert.That(_reader.Read(SessionId)!.InFlight, Is.False);
    }

    [Test]
    public void Read_TakesTheRunThatIsActuallyMoving()
    {
        Journal("wf_old", started: 5, finished: 5, written: DateTimeOffset.UtcNow.AddHours(-6));
        Journal("wf_now", started: 33, finished: 12, written: DateTimeOffset.UtcNow);

        Assert.That(_reader.Read(SessionId)!.RunId, Is.EqualTo("wf_now"));
    }

    [Test]
    public void Silence_MeasuresHowLongTheJournalHasStoodStill()
    {
        var written = DateTimeOffset.UtcNow.AddMinutes(-40);
        Journal("wf_stalled", started: 33, finished: 12, written: written);

        var activity = _reader.Read(SessionId)!;

        Assert.That(activity.Silence(DateTimeOffset.UtcNow).TotalMinutes, Is.EqualTo(40).Within(2),
            "a run whose agents are all outstanding looks the same as one killed mid-flight");
    }

    [Test]
    public void Read_FindsNothingForASessionThatHasRunNoWorkflow()
    {
        Assert.That(_reader.Read("11111111-2222-3333-4444-555555555555"), Is.Null);
        Assert.That(_reader.Read(null), Is.Null);
    }

    [Test]
    public void Read_IgnoresARunDirectoryWithNoJournalYet()
    {
        Directory.CreateDirectory(Path.Combine(_runs, "wf_empty"));

        Assert.That(_reader.Read(SessionId), Is.Null);
    }

    [Test]
    public void Read_CanCountAJournalThatIsBeingWrittenTo()
    {
        var path = Path.Combine(_runs, "wf_live", "journal.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Lines(4, 1));

        using var held = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

        Assert.That(_reader.Read(SessionId)!.Started, Is.EqualTo(4),
            "the workflow holds the journal open for the whole run");
    }

    private void Journal(string runId, int started, int finished, DateTimeOffset? written = null)
    {
        var path = Path.Combine(_runs, runId, "journal.jsonl");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, Lines(started, finished));

        if (written is not null)
        {
            File.SetLastWriteTimeUtc(path, written.Value.UtcDateTime);
        }
    }

    private static string Lines(int started, int finished)
    {
        var lines = Enumerable.Repeat("""{"type":"started","key":"v2:abc","agentId":"a1"}""", started)
            .Concat(Enumerable.Repeat("""{"type":"result","key":"v2:abc","agentId":"a1","result":{}}""", finished));

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    [Test]
    public void IsRunning_IsFalseForARunThatWroteItsSummaryAndIsOver()
    {
        Journal("wf_over", started: 10, finished: 9);
        Summary("wf_over");

        var activity = _reader.Read(SessionId)!;

        Assert.That(activity.InFlight, Is.True, "the gap outlives the run that left it");
        Assert.That(activity.IsRunning(DateTimeOffset.UtcNow), Is.False,
            "a run killed with an agent outstanding would otherwise claim to be going for ever");
    }

    [Test]
    public void IsRunning_IsFalseOnceTheJournalHasStoppedGrowing()
    {
        Journal("wf_stalled", started: 33, finished: 12,
            written: DateTimeOffset.UtcNow - WorkflowActivity.Patience.Add(TimeSpan.FromMinutes(1)));

        Assert.That(_reader.Read(SessionId)!.IsRunning(DateTimeOffset.UtcNow), Is.False,
            "no summary is written when a session is killed outright, so silence is the other tell");
    }

    [Test]
    public void IsRunning_IsTrueWhileAgentsAreOutAndTheJournalIsStillMoving()
    {
        Journal("wf_live", started: 33, finished: 12, written: DateTimeOffset.UtcNow);

        Assert.That(_reader.Read(SessionId)!.IsRunning(DateTimeOffset.UtcNow), Is.True);
    }

    [Test]
    public void IsRunning_IsFalseWhenEveryAgentHasComeBack()
    {
        Journal("wf_done", started: 5, finished: 5, written: DateTimeOffset.UtcNow);

        Assert.That(_reader.Read(SessionId)!.IsRunning(DateTimeOffset.UtcNow), Is.False);
    }

    private void Summary(string runId)
    {
        var session = Path.Combine(_root, "C--dev-tfsgit", SessionId);

        Directory.CreateDirectory(Path.Combine(session, "workflows"));
        File.WriteAllText(Path.Combine(session, "workflows", $"{runId}.json"), """{"status":"killed"}""");
    }
}
