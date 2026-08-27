using System.Text.Json;
using Theseus.Core.Persistence;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

[TestFixture]
public class StopHookInputTests
{
    private const string Real =
        """
        {"session_id":"11111111-2222-3333-4444-555555555555","transcript_path":"/c/x.jsonl",
         "cwd":"/c/work","hook_event_name":"Stop","stop_hook_active":false}
        """;

    [Test]
    public void Parsing_ReadsTheFieldsClaudeCodeActuallySends()
    {
        var input = JsonSerializer.Deserialize<StopHookInput>(Real, TheseusJson.Compact)!;

        Assert.That(input.SessionId, Is.EqualTo("11111111-2222-3333-4444-555555555555"));
        Assert.That(input.HookEventName, Is.EqualTo("Stop"));
        Assert.That(input.StopHookActive, Is.False);
    }

    [Test]
    public void Parsing_IgnoresEveryFieldItDoesNotNeed()
    {
        Assert.That(JsonSerializer.Deserialize<StopHookInput>(Real, TheseusJson.Compact), Is.Not.Null,
            "a field added by a later build must not break the parse");
    }

    [Test]
    public void Parsing_ReadsASubagentsIdentifier()
    {
        const string json = """{"session_id":"11111111-2222-3333-4444-555555555555","agent_id":"a1"}""";

        Assert.That(JsonSerializer.Deserialize<StopHookInput>(json, TheseusJson.Compact)!.AgentId, Is.EqualTo("a1"));
    }

    [Test]
    public void Parsing_ReadsATurnAlreadyExtendedByAStopHook()
    {
        const string json = """{"session_id":"x","stop_hook_active":true}""";

        Assert.That(JsonSerializer.Deserialize<StopHookInput>(json, TheseusJson.Compact)!.StopHookActive, Is.True);
    }
}
