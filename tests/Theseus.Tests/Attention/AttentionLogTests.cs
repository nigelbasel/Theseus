using Theseus.Core.Attention;

namespace Theseus.Tests.Attention;

[TestFixture]
public class AttentionLogTests
{
    private string _path = string.Empty;

    [SetUp]
    public void SetUp() =>
        _path = Path.Combine(Path.GetTempPath(), $"theseus-attention-{Guid.NewGuid():N}", "attention.jsonl");

    [TearDown]
    public void TearDown()
    {
        if (Path.GetDirectoryName(_path) is { } directory && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Append_KeepsWhatWasWrittenBefore()
    {
        var log = new AttentionLog(_path);
        var start = new DateTimeOffset(2026, 9, 15, 13, 0, 0, TimeSpan.Zero);
        var first = new AttentionInterval(AttentionKind.Attended, "alpha", start, start.AddMinutes(3));
        var second = new AttentionInterval(AttentionKind.Machine, "beta", start, start.AddMinutes(10));

        log.Append([first]);
        log.Append([second]);

        Assert.That(new AttentionLog(_path).Read(), Is.EqualTo(new[] { first, second }));
    }

    [Test]
    public void Read_SkipsALineItCannotReadAndKeepsTheRest()
    {
        var log = new AttentionLog(_path);
        var start = new DateTimeOffset(2026, 9, 15, 13, 0, 0, TimeSpan.Zero);
        var interval = new AttentionInterval(AttentionKind.Attended, "alpha", start, start.AddMinutes(1));

        log.Append([interval]);
        File.AppendAllText(_path, "{ not json\n");

        Assert.That(log.Read(), Is.EqualTo(new[] { interval }));
    }

    [Test]
    public void Read_GivesNothingBeforeAnythingIsWritten()
    {
        Assert.That(new AttentionLog(_path).Read(), Is.Empty);
    }
}
