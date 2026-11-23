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

            Assert.That(TranscriptTail.LastAssistantText(path), Is.EqualTo("Shall I push?"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void AMissingFile_HasNoText()
    {
        Assert.That(TranscriptTail.LastAssistantText(Path.Combine(Path.GetTempPath(), $"absent-{Guid.NewGuid():N}.jsonl")), Is.Null);
        Assert.That(TranscriptTail.LastAssistantText(null), Is.Null);
    }

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
