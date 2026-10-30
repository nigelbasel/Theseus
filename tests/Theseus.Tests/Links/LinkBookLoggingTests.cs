using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;
using Theseus.Core.Links;

namespace Theseus.Tests.Links;

[TestFixture]
public class LinkBookLoggingTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private LinkBook _book = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-link-log-{Guid.NewGuid():N}");
        TheseusLog.Start(Path.Combine(_directory, "logs"));
        _book = new LinkBook(new LinkStateStore(Path.Combine(_directory, "links.json")), new FixedClock(Nine));
    }

    [TearDown]
    public void TearDown()
    {
        TheseusLog.Stop();

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void Add_LogsALinkMade()
    {
        var link = _book.Add(LinkKind.HandsOver, "token-spike", "auth-implementation", output: @"C:\work\REPORT.md").Link!;

        Assert.That(Log(), Does.Contain($"Link {link.Id} made by board: HandsOver token-spike -> auth-implementation, output C:\\work\\REPORT.md"));
    }

    [Test]
    public void Add_LogsARefusalWithItsReason()
    {
        _book.Add(LinkKind.HandsOver, "auth-research", "token-spike");

        _book.Add(LinkKind.HandsOver, "other-card", "token-spike");

        Assert.That(Log(), Does.Contain(
            "Link HandsOver other-card -> token-spike refused: auth-research already hands over to token-spike; a card takes over from one card only."));
    }

    [Test]
    public void Remove_LogsTheLinkRemoved()
    {
        var link = _book.Add(LinkKind.HandsOver, "auth-research", "token-spike").Link!;

        _book.Remove(link.Id);

        Assert.That(Log(), Does.Contain($"Link {link.Id} removed"));
    }

    [Test]
    public void Satisfy_LogsARefusalWithItsReason()
    {
        var link = _book.Add(LinkKind.HandsOver, "auth-research", "token-spike").Link!;

        _book.Satisfy(link.Id, "read it");

        Assert.That(Log(), Does.Contain($"Link {link.Id} not satisfied: That link names no document, so there is nothing to accept."));
    }

    private string Log()
    {
        TheseusLog.Stop();

        return string.Join(Environment.NewLine,
            Directory.GetFiles(Path.Combine(_directory, "logs")).Select(File.ReadAllText));
    }

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
