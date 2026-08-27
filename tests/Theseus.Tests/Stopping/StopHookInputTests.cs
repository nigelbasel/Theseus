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

    [Test]
    public void Parse_ToleratesAByteOrderMarkOnStandardInput()
    {
        var input = StopHookInput.Parse("﻿" + Real);

        Assert.That(input?.SessionId, Is.EqualTo("11111111-2222-3333-4444-555555555555"),
            "a hook that silently does nothing when fed a mark gives no clue why; this already cost a session");
    }

    [Test]
    public void Parse_ReadsOrdinaryInput()
    {
        Assert.That(StopHookInput.Parse(Real)?.HookEventName, Is.EqualTo("Stop"));
    }

    [Test]
    public void Parse_ReturnsNothingRatherThanThrowingOnRubbish()
    {
        Assert.That(StopHookInput.Parse("{ half a message"), Is.Null);
        Assert.That(StopHookInput.Parse(string.Empty), Is.Null);
        Assert.That(StopHookInput.Parse(null), Is.Null);
    }
}
