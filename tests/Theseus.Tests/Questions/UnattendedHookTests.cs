using System.Text.Json;
using Theseus.Core.Questions;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Questions;

/// <summary>What the hook refuses a card of an unattended project (2.4 W2): the question tool, and one prose question.</summary>
[TestFixture]
public class UnattendedHookTests
{
    private const string SessionId = "11111111-2222-3333-4444-555555555555";

    [Test]
    public void TheQuestionTool_IsRefused_AndTheCardIsToldToPark()
    {
        var reply = UnattendedHook.PreToolUseReply("AskUserQuestion");

        Assert.That(reply, Is.Not.Null);

        var output = JsonDocument.Parse(reply!).RootElement.GetProperty("hookSpecificOutput");

        Assert.Multiple(() =>
        {
            Assert.That(output.GetProperty("hookEventName").GetString(), Is.EqualTo("PreToolUse"));
            Assert.That(output.GetProperty("permissionDecision").GetString(), Is.EqualTo("deny"));
            Assert.That(output.GetProperty("permissionDecisionReason").GetString(), Does.Contain("ask_owner").And.Contain("park"));
        });
    }

    [Test]
    public void AnyOtherTool_IsLeftAlone()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UnattendedHook.PreToolUseReply("Bash"), Is.Null);
            Assert.That(UnattendedHook.PreToolUseReply("mcp__theseus__ask_owner"), Is.Null);
            Assert.That(UnattendedHook.PreToolUseReply(null), Is.Null);
        });
    }

    [Test]
    public void ATurnEndingOnAProseQuestion_IsRefusedOnce_PointingAtAskOwner()
    {
        var refusal = UnattendedHook.StopRefusal(Stop("Done the fix. Shall I raise the PR now?"), Tail(Prompt("go"), Assistant(Text("Done."))));

        Assert.That(refusal, Does.Contain("ask_owner").And.Contain("report_back"));
    }

    [Test]
    public void ATurnAlreadyExtendedByAStopHook_IsNotRefusedAgain()
    {
        var input = Stop("Shall I raise the PR now?") with { StopHookActive = true };

        Assert.That(UnattendedHook.StopRefusal(input, Tail(Prompt("go"))), Is.Null, "refused once, never twice");
    }

    [Test]
    public void ASubagentsTurn_OrATurnThatAsksNothing_IsNotRefused()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UnattendedHook.StopRefusal(Stop("Shall I go on?") with { AgentId = "agent-1" }, Tail(Prompt("go"))), Is.Null);
            Assert.That(UnattendedHook.StopRefusal(Stop("The fix is merged and the suite is green."), Tail(Prompt("go"))), Is.Null);
            Assert.That(UnattendedHook.StopRefusal(Stop("Shall I go on?") with { HookEventName = "UserPromptSubmit" }, Tail(Prompt("go"))), Is.Null);
        });
    }

    [Test]
    public void ATurnThatCalledAskOwner_OrReportBack_MayEndOnItsQuestion()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UnattendedHook.StopRefusal(Stop("Which option do you want?"),
                Tail(Prompt("go"), Assistant(ToolUse("mcp__theseus__ask_owner")), ToolResult())), Is.Null);
            Assert.That(UnattendedHook.StopRefusal(Stop("Do you want me to start the next one?"),
                Tail(Prompt("go"), Assistant(ToolUse("mcp__theseus__report_back")), ToolResult())), Is.Null);
        });
    }

    [Test]
    public void AskOwnerCalledInAnEarlierTurn_DoesNotExcuseThisOne()
    {
        var tail = Tail(Prompt("first"), Assistant(ToolUse("mcp__theseus__ask_owner")), ToolResult(), Prompt("theseus-next"),
            Assistant(Text("Working.")));

        Assert.That(UnattendedHook.StopRefusal(Stop("Shall I carry on?"), tail), Is.Not.Null);
    }

    [Test]
    public void TheTurnsLastWords_AreReadFromTheTranscript_WhenClaudeCodeDoesNotSendThem()
    {
        var tail = Tail(Prompt("go"), Assistant(Text("Built it. Do you want me to push?")));

        Assert.That(UnattendedHook.StopRefusal(Stop(null), tail), Is.Not.Null);
    }

    [Test]
    public void ToolsCalledThisTurn_StartAfterTheLastPromptTyped_NotAfterAToolResult()
    {
        var tail = Tail(Prompt("first"), Assistant(ToolUse("Read")), ToolResult(), Prompt("second"),
            Assistant(ToolUse("Bash")), ToolResult(), Assistant(ToolUse("mcp__theseus__now_at")), ToolResult());

        Assert.That(TranscriptTail.ToolsCalledThisTurn(tail(null)), Is.EquivalentTo(new[] { "Bash", "mcp__theseus__now_at" }));
    }

    private static StopHookInput Stop(string? lastWords) =>
        new(SessionId, "Stop", LastAssistantMessage: lastWords, TranscriptPath: "t.jsonl");

    private static Func<string?, string?> Tail(params string[] lines) => _ => string.Join('\n', lines);

    private static string Prompt(string text) =>
        JsonSerializer.Serialize(new { type = "user", message = new { role = "user", content = text } });

    private static string ToolResult() =>
        JsonSerializer.Serialize(new { type = "user", message = new { role = "user", content = new object[] { new { type = "tool_result", tool_use_id = "t1", content = "ok" } } } });

    private static string Assistant(object block) =>
        JsonSerializer.Serialize(new { type = "assistant", message = new { role = "assistant", content = new[] { block } } });

    private static object Text(string text) => new { type = "text", text };

    private static object ToolUse(string name) => new { type = "tool_use", id = "t1", name, input = new { } };
}
