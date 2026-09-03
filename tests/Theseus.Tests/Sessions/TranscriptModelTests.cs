using System.Text;
using Theseus.App.Services;

namespace Theseus.Tests.Sessions;

/// <summary>
/// Reading the model off the end of a transcript, on files of the shapes that actually occur.
///
/// The tail is read first because a live transcript grows every turn and runs to tens of megabytes;
/// the fallback exists for the one shape the tail cannot handle, a last turn buried under a tool
/// result bigger than the window. Both paths are exercised here on real files, because the
/// arithmetic of "read the last half-megabyte and throw away the partial first line" is exactly the
/// kind that looks right and is off by one.
/// </summary>
[TestFixture]
public class TranscriptModelTests
{
    private string _path = string.Empty;

    [SetUp]
    public void SetUp() => _path = Path.Combine(Path.GetTempPath(), $"theseus-transcript-{Guid.NewGuid():N}.jsonl");

    [TearDown]
    public void TearDown() => File.Delete(_path);

    [Test]
    public void LastModel_TakesTheLastRealTurnNotTheFirst()
    {
        Write(
            Assistant("claude-opus-5"),
            User("switching now"),
            Assistant("claude-fable-5-1"));

        Assert.That(Read(), Is.EqualTo("claude-fable-5-1"),
            "a switch part-way through must show as the switch, not as what the session started on");
    }

    [Test]
    public void LastModel_SkipsASyntheticTurnAtTheEnd()
    {
        Write(
            Assistant("claude-opus-5"),
            Assistant("<synthetic>"));

        Assert.That(Read(), Is.EqualTo("claude-opus-5"),
            "the harness's own acknowledgement turn is the last line more often than not");
    }

    [Test]
    public void LastModel_FallsBackToAFullReadWhenTheLastTurnIsBuriedUnderAHugeToolResult()
    {
        // A tool result well past the half-megabyte window, after the only assistant turn.
        Write(
            Assistant("claude-sonnet-5"),
            User(new string('x', 700 * 1024)));

        Assert.That(Read(), Is.EqualTo("claude-sonnet-5"),
            "the tail read sees only the tool result; the fallback must find the turn before it");
    }

    [Test]
    public void LastModel_IsNullForATranscriptWithNoRealTurnYet()
    {
        Write(User("first message, nothing answered yet"));

        Assert.That(Read(), Is.Null);
    }

    [Test]
    public void LastModel_IsNullForAnEmptyFile()
    {
        File.WriteAllText(_path, string.Empty);

        Assert.That(Read(), Is.Null);
    }

    [Test]
    public void LastModel_CopesWithATailWindowThatStartsMidLine()
    {
        // Enough padding that the 512 KB window opens part-way through an earlier line, which the
        // reader must discard rather than parse as a fragment.
        var lines = Enumerable.Range(0, 40).Select(_ => User(new string('p', 20 * 1024))).ToList();

        lines.Add(Assistant("claude-haiku-4-5-20251001"));
        Write([.. lines]);

        Assert.That(Read(), Is.EqualTo("claude-haiku-4-5-20251001"));
    }

    private string? Read() => TranscriptModel.LastModel(_path, new FileInfo(_path).Length);

    private void Write(params string[] lines) =>
        File.WriteAllText(_path, string.Join('\n', lines) + '\n', new UTF8Encoding(false));

    private static string Assistant(string model) =>
        $$$"""{"type":"assistant","message":{"model":"{{{model}}}","role":"assistant","content":[]}}""";

    private static string User(string text) =>
        $$$"""{"type":"user","message":{"role":"user","content":"{{{text}}}"}}""";
}
