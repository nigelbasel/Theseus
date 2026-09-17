using Theseus.Core.Attention;

namespace Theseus.Tests.Attention;

[TestFixture]
public class WorkLogTests
{
    private string _path = string.Empty;

    [SetUp]
    public void SetUp() =>
        _path = Path.Combine(Path.GetTempPath(), $"theseus-work-{Guid.NewGuid():N}", "work.jsonl");

    [TearDown]
    public void TearDown()
    {
        if (Path.GetDirectoryName(_path) is { } directory && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Append_KeepsEveryMarkInTheOrderWritten()
    {
        var at = new DateTimeOffset(2026, 9, 17, 9, 0, 0, TimeSpan.Zero);
        var start = new WorkMark("s1", "gdpr", WorkMarkKind.Start, "NDC-680", "Fixing the export", at);
        var stop = new WorkMark("s1", "gdpr", WorkMarkKind.Stop, null, null, at.AddMinutes(40));

        new WorkLog(_path).Append(start);
        new WorkLog(_path).Append(stop);

        Assert.That(new WorkLog(_path).Read(), Is.EqualTo(new[] { start, stop }));
    }

    [Test]
    public void Read_SkipsALineItCannotReadAndKeepsTheRest()
    {
        var log = new WorkLog(_path);
        var mark = new WorkMark("s1", "gdpr", WorkMarkKind.Start, "NDC-680", null, DateTimeOffset.UnixEpoch);

        log.Append(mark);
        File.AppendAllText(_path, "{ not json\n");

        Assert.That(log.Read(), Is.EqualTo(new[] { mark }));
    }
}
