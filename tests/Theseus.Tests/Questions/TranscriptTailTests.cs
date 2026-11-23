using System.Text.Json.Nodes;
using Theseus.Core.Questions;

namespace Theseus.Tests.Questions;

[TestFixture]
public class TranscriptTailTests
{
    [Test]
    public void TheLastAssistantText_IsWhatTheTurnEndedOn()
    {
        var lines = Join(Assistant("text", "First."), User("thanks"), Assistant("text", "Shall I push?"), Other("system"));

        Assert.That(TranscriptTail.LastAssistantTextIn(lines), Is.EqualTo("Shall I push?"));
    }

    [Test]
    public void ATurnEndingOnAToolCall_HasNoText()
    {
        var lines = Join(Assistant("text", "Shall I push?"), Assistant("tool_use", null));

        Assert.That(TranscriptTail.LastAssistantTextIn(lines), Is.Null, "the question tool is a tool call, and needs no guess");
    }

    [Test]
    public void ASubagentsWords_AreNotTheSessions()
    {
        var lines = Join(Assistant("text", "Done."), Assistant("text", "Which file?", sidechain: true));

        Assert.That(TranscriptTail.LastAssistantTextIn(lines), Is.EqualTo("Done."));
    }

    [Test]
    public void APartLineAtTheWindowsStart_IsPassedOver()
    {
        var lines = "ntent\":[{\"type\":\"text\"}]}\n" + Assistant("text", "Done.");

        Assert.That(TranscriptTail.LastAssistantTextIn(lines), Is.EqualTo("Done."));
    }

    [Test]
    public void OnlyTheEndOfALongFileIsRead()
    {
        var path = Path.Combine(Path.GetTempPath(), $"theseus-transcript-{Guid.NewGuid():N}.jsonl");

        try
        {
            File.WriteAllText(path, Join(Assistant("text", "Early."), Assistant("text", new string('x', TranscriptTail.Window)), Assistant("text", "Shall I push?")));

            var tail = TranscriptTail.Read(path)!;

            Assert.That(tail.Length, Is.LessThanOrEqualTo(TranscriptTail.Window));
            Assert.That(TranscriptTail.LastAssistantTextIn(tail), Is.EqualTo("Shall I push?"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void AMissingFile_ReadsAsNothing()
    {
        Assert.That(TranscriptTail.Read(Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.jsonl")), Is.Null);
        Assert.That(TranscriptTail.Read(null), Is.Null);
        Assert.That(TranscriptTail.LastAssistantTextIn(null), Is.Null);
    }

    [Test]
    public void TheQuestionTool_CountsAsAnswered_OnlyByItsOwnResult_AtOrAfterTheMoment()
    {
        var at = new DateTimeOffset(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);
        var lines = Join(
            Tool("assistant", at, new JsonObject { ["type"] = "tool_use", ["id"] = "q", ["name"] = "AskUserQuestion" }),
            Tool("assistant", at, new JsonObject { ["type"] = "tool_use", ["id"] = "b", ["name"] = "Bash" }),
            Tool("user", at.AddMinutes(1), new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = "b" }),
            Tool("user", at.AddMinutes(2), new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = "q" }));

        Assert.That(TranscriptTail.QuestionToolAnsweredSince(lines, at.AddMinutes(2)), Is.True);
        Assert.That(TranscriptTail.QuestionToolAnsweredSince(lines, at.AddMinutes(3)), Is.False, "an answer from before the question is not its answer");
        Assert.That(TranscriptTail.QuestionToolAnsweredSince(Join(Tool("user", at.AddMinutes(1), new JsonObject { ["type"] = "tool_result", ["tool_use_id"] = "b" })), at),
            Is.False, "another tool's result is no answer");
    }

    private static string Tool(string type, DateTimeOffset at, JsonObject block) =>
        new JsonObject
        {
            ["type"] = type,
            ["timestamp"] = at.ToString("O"),
            ["message"] = new JsonObject { ["content"] = new JsonArray(block) },
        }.ToJsonString();

    private static string Join(params string[] lines) => string.Join('\n', lines) + "\n";

    private static string Assistant(string kind, string? text, bool sidechain = false)
    {
        var block = new JsonObject { ["type"] = kind };

        if (text is not null)
        {
            block["text"] = text;
        }

        return new JsonObject
        {
            ["type"] = "assistant",
            ["isSidechain"] = sidechain,
            ["message"] = new JsonObject { ["content"] = new JsonArray(block) },
        }.ToJsonString();
    }

    private static string User(string text) =>
        new JsonObject { ["type"] = "user", ["message"] = new JsonObject { ["content"] = text } }.ToJsonString();

    private static string Other(string type) => new JsonObject { ["type"] = type }.ToJsonString();
}
