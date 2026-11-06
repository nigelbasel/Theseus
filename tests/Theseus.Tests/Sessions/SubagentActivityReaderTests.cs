using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class SubagentActivityReaderTests
{
    private const string SessionId = "1da208a6-9aa1-4585-a89b-8ce7c5c14fce";

    private string _root = string.Empty;
    private string _subagents = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-subagents-{Guid.NewGuid():N}");
        _subagents = Path.Combine(_root, "E--dev-tfsgit", SessionId, "subagents");
        Directory.CreateDirectory(Path.Combine(_subagents, "workflows", "wf_1"));
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Running_CountsTranscriptsWrittenInTheLastTwoMinutes_AndNotWorkflowAgents()
    {
        var now = DateTimeOffset.UtcNow;
        Transcript("agent-a.jsonl", now.AddSeconds(-10));
        Transcript("agent-b.jsonl", now.AddSeconds(-90));
        Transcript("agent-c.jsonl", now.AddMinutes(-10));
        Transcript(Path.Combine("workflows", "wf_1", "agent-d.jsonl"), now);

        Assert.That(new SubagentActivityReader(_root).Running(SessionId, now), Is.EqualTo(2));
    }

    [Test]
    public void Running_IsNothing_ForASessionWithNoSubagents()
    {
        var reader = new SubagentActivityReader(_root);

        Assert.That(reader.Running("someone-else", DateTimeOffset.UtcNow), Is.Zero);
        Assert.That(reader.Running(null, DateTimeOffset.UtcNow), Is.Zero);
    }

    private void Transcript(string name, DateTimeOffset written)
    {
        var path = Path.Combine(_subagents, name);
        File.WriteAllText(path, "{}\n");
        File.SetLastWriteTimeUtc(path, written.UtcDateTime);
    }
}
