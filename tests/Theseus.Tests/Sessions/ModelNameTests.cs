using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

/// <summary>
/// Reading a session's model off its transcript, and saying it plainly on a card.
///
/// The transcript is the only place the model is recorded: the registry has no field for it, and a
/// session's own idea of its model is fixed at start and does not learn about a switch. Nigel
/// switched this workstream's own session from Opus to Fable mid-conversation on 2026-09-03, and the
/// transcript was the one thing that showed it.
/// </summary>
[TestFixture]
public class ModelNameTests
{
    [Test]
    public void FromTranscriptLine_ReadsTheModelOffAnAssistantTurn()
    {
        const string line = """{"type":"assistant","message":{"model":"claude-fable-5-1","content":[]},"uuid":"x"}""";

        Assert.That(ModelName.FromTranscriptLine(line), Is.EqualTo("claude-fable-5-1"));
    }

    [Test]
    public void FromTranscriptLine_IgnoresATurnTheHarnessWroteItself()
    {
        const string line = """{"type":"assistant","message":{"model":"<synthetic>","content":[]}}""";

        Assert.That(ModelName.FromTranscriptLine(line), Is.Null,
            "a synthetic turn says nothing about which model is running");
    }

    [Test]
    public void FromTranscriptLine_IgnoresAUserTurn()
    {
        const string line = """{"type":"user","message":{"role":"user","content":"hello"}}""";

        Assert.That(ModelName.FromTranscriptLine(line), Is.Null);
    }

    [Test]
    public void FromTranscriptLine_IsNotFooledByATurnThatTalksAboutModels()
    {
        // An assistant explaining this very feature writes the words "model":"..." in its text.
        // Searching the line for them would read the example as the answer.
        const string line = """{"type":"user","message":{"content":"the transcript has \"model\":\"claude-opus-5\" on each turn"}}""";

        Assert.That(ModelName.FromTranscriptLine(line), Is.Null);
    }

    [Test]
    public void FromTranscriptLine_ShrugsAtALineThatIsNotJson()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ModelName.FromTranscriptLine("{\"type\":\"assistant\", broken"), Is.Null);
            Assert.That(ModelName.FromTranscriptLine(string.Empty), Is.Null);
            Assert.That(ModelName.FromTranscriptLine(null), Is.Null);
        });
    }

    [TestCase("claude-opus-5", "Opus 5")]
    [TestCase("claude-fable-5-1", "Fable 5.1")]
    [TestCase("claude-sonnet-5", "Sonnet 5")]
    [TestCase("claude-haiku-4-5-20251001", "Haiku 4.5")]
    public void Friendly_NamesTheFamilyAndVersion(string id, string expected)
    {
        Assert.That(ModelName.Friendly(id), Is.EqualTo(expected));
    }

    [Test]
    public void Friendly_ShowsAnIdentifierItDoesNotRecogniseAsItIs()
    {
        Assert.That(ModelName.Friendly("gpt-oss-20b"), Is.EqualTo("gpt-oss-20b"),
            "better to show a strange name than to invent a familiar one");
    }

    [Test]
    public void Friendly_SaysNothingForNothing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ModelName.Friendly(null), Is.Empty);
            Assert.That(ModelName.Friendly("  "), Is.Empty);
        });
    }
}
