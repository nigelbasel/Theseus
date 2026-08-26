using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class WorkflowRunReaderTests
{
    private const string SessionId = "7d73cb6d-9784-4167-9235-8d84008e3500";

    private string _root = string.Empty;
    private string _workflows = string.Empty;
    private WorkflowRunReader _reader = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-wf-{Guid.NewGuid():N}");
        _workflows = Path.Combine(_root, "C--dev-tfsgit", SessionId, "workflows");

        Directory.CreateDirectory(_workflows);
        Directory.CreateDirectory(Path.Combine(_root, "C--dev-somewhere-else"));

        _reader = new WorkflowRunReader(_root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Read_ReadsARecordInTheShapeClaudeCodeWritesIt()
    {
        Write("wf_e606be63-cda", """
            {
              "runId": "wf_e606be63-cda",
              "timestamp": "2026-08-26T10:14:13.569Z",
              "script": "export const meta = { name: 'nexus-instance-scripts' }",
              "agentCount": 17,
              "durationMs": 1851251,
              "summary": "Instance-DB scripts for DbScriptRunner",
              "workflowName": "nexus-instance-scripts",
              "status": "killed",
              "phases": [
                { "title": "Author", "detail": "write each instance script" },
                { "title": "Runbook", "detail": "write the runbook" }
              ],
              "defaultModel": "claude-opus-5[1m]",
              "totalTokens": 2235969,
              "totalToolCalls": 429
            }
            """);

        var run = _reader.Read(SessionId).Single();

        Assert.That(run.WorkflowName, Is.EqualTo("nexus-instance-scripts"));
        Assert.That(run.AgentCount, Is.EqualTo(17));
        Assert.That(run.TotalTokens, Is.EqualTo(2235969));
        Assert.That(run.Status, Is.EqualTo("killed"));
        Assert.That(run.Completed, Is.False);
        Assert.That(run.Phases.Select(phase => phase.Title), Is.EqualTo(new[] { "Author", "Runbook" }));
    }

    [Test]
    public void Read_DescribesARunInOneLine()
    {
        Write("wf_a", """{"runId":"wf_a","workflowName":"nexus-sql-split","agentCount":51,"totalTokens":2235969,"status":"killed"}""");

        Assert.That(_reader.Read(SessionId).Single().Describe,
            Is.EqualTo("nexus-sql-split · 51 agents · 2.2M tokens · killed"));
    }

    [Test]
    public void Read_PutsTheMostRecentRunFirst()
    {
        Write("wf_old", """{"runId":"wf_old","workflowName":"first","timestamp":"2026-08-26T09:00:00.000Z"}""");
        Write("wf_new", """{"runId":"wf_new","workflowName":"second","timestamp":"2026-08-26T11:14:00.000Z"}""");

        Assert.That(_reader.Read(SessionId).Select(run => run.WorkflowName),
            Is.EqualTo(new[] { "second", "first" }));
    }

    [Test]
    public void Read_FindsTheSessionWithoutBeingToldWhichProjectItBelongsTo()
    {
        Write("wf_a", """{"runId":"wf_a","workflowName":"found"}""");

        Assert.That(_reader.Read(SessionId), Has.Count.EqualTo(1),
            "the project folder name mangles a path the session may no longer be in");
    }

    [Test]
    public void Read_SkipsARecordItCannotUnderstandRatherThanLosingTheRest()
    {
        Write("wf_good", """{"runId":"wf_good","workflowName":"good"}""");
        Write("wf_bad", "{ half a file");

        Assert.That(_reader.Read(SessionId).Select(run => run.WorkflowName), Is.EqualTo(new[] { "good" }));
    }

    [Test]
    public void Read_FindsNothingForASessionThatHasRunNoWorkflows()
    {
        Assert.That(_reader.Read("11111111-2222-3333-4444-555555555555"), Is.Empty);
        Assert.That(_reader.Read(null), Is.Empty);
    }

    [Test]
    public void Read_TreatsAMissingPhaseListAsNoPhasesRatherThanFailing()
    {
        Write("wf_a", """{"runId":"wf_a","workflowName":"no-phases"}""");

        Assert.That(_reader.Read(SessionId).Single().Phases, Is.Empty);
    }

    private void Write(string name, string json) =>
        File.WriteAllText(Path.Combine(_workflows, $"{name}.json"), json);
}
