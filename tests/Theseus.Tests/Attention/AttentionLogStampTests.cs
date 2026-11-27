using Theseus.Core.Attention;

namespace Theseus.Tests.Attention;

/// <summary>7.3: a stamped line reads back with every field, and a line from before 7.3 still reads.</summary>
[TestFixture]
public class AttentionLogStampTests
{
    private string _path = null!;

    [SetUp]
    public void SetUp() => _path = Path.Combine(Path.GetTempPath(), $"theseus-attention-{Guid.NewGuid():N}", "attention.jsonl");

    [TearDown]
    public void TearDown()
    {
        if (Path.GetDirectoryName(_path) is { } folder && Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Test]
    public void AStampedLine_ReadsBackWithEveryField()
    {
        var line = new AttentionInterval(AttentionKind.Attended, "alpha", new DateTimeOffset(2026, 11, 27, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 27, 9, 5, 0, TimeSpan.Zero))
        {
            Session = "s1",
            Project = "theseus",
            PieceId = "piece-1",
            Stage = "Implement",
            Offset = "+02:00",
            Source = "theseus",
        };
        var log = new AttentionLog(_path);

        log.Append([line]);

        Assert.That(log.Read(), Is.EqualTo(new[] { line }));
    }

    [Test]
    public void ALineFromBefore73_StillReads_WithNoStamp()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path,
            "{\"kind\":\"Machine\",\"workstream\":\"git-22\",\"start\":\"2026-11-27T17:16:29+00:00\",\"end\":\"2026-11-27T17:16:44+00:00\"}\n");

        var read = new AttentionLog(_path).Read().Single();

        Assert.That((read.Workstream, read.Session, read.PieceId, read.Offset), Is.EqualTo(("git-22", (string?)null, (string?)null, (string?)null)));
    }

    [Test]
    public void AnUnstampedLine_IsWrittenWithoutEmptyFields()
    {
        new AttentionLog(_path).Append([new AttentionInterval(AttentionKind.Machine, "a", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddSeconds(1))]);

        Assert.That(File.ReadAllText(_path), Does.Not.Contain("null"));
    }
}
