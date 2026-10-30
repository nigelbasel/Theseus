using Theseus.App.Services;

namespace Theseus.Tests.Launcher;

/// <summary>
/// A session's transcript is found one level under the projects folder. Searching the whole tree,
/// subagent and workflow transcripts included, made the board's refresh take up to 24 seconds.
/// </summary>
[TestFixture]
public class TranscriptLocatorTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp() => _root = Directory.CreateTempSubdirectory("theseus-transcripts-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Find_LooksInEachProjectFolder()
    {
        var project = Directory.CreateDirectory(Path.Combine(_root, "C--dev-git")).FullName;
        File.WriteAllText(Path.Combine(project, "session-1.jsonl"), "{}");

        Assert.That(TranscriptLocator.Find("session-1", _root), Is.EqualTo(Path.Combine(project, "session-1.jsonl")));
    }

    [Test]
    public void Find_DoesNotGoDeeperThanTheProjectFolder()
    {
        var deep = Directory.CreateDirectory(Path.Combine(_root, "C--dev-git", "parent", "subagents")).FullName;
        File.WriteAllText(Path.Combine(deep, "session-2.jsonl"), "{}");

        Assert.That(TranscriptLocator.Find("session-2", _root), Is.Null);
    }

    [Test]
    public void Find_GivesNothingForNoSessionOrNoFolder()
    {
        Assert.That(TranscriptLocator.Find(null, _root), Is.Null);
        Assert.That(TranscriptLocator.Find("session-3", Path.Combine(_root, "missing")), Is.Null);
    }

    [Test]
    public void Find_NoticesATranscriptThatHasMoved()
    {
        var first = Directory.CreateDirectory(Path.Combine(_root, "first")).FullName;
        File.WriteAllText(Path.Combine(first, "session-4.jsonl"), "{}");
        TranscriptLocator.Find("session-4", _root);

        File.Delete(Path.Combine(first, "session-4.jsonl"));
        var second = Directory.CreateDirectory(Path.Combine(_root, "second")).FullName;
        File.WriteAllText(Path.Combine(second, "session-4.jsonl"), "{}");

        Assert.That(TranscriptLocator.Find("session-4", _root), Is.EqualTo(Path.Combine(second, "session-4.jsonl")));
    }
}
