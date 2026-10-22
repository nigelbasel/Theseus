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
        var link = _book.Add(LinkKind.Feeds, "token-spike", "auth-implementation", output: @"C:\work\REPORT.md").Link!;

        Assert.That(Log(), Does.Contain($"Link {link.Id} made: Feeds token-spike -> auth-implementation, output C:\\work\\REPORT.md"));
    }

    [Test]
    public void Add_LogsARefusalWithItsReason()
    {
        _book.Add(LinkKind.Spawned, "auth-research", "token-spike");

        _book.Add(LinkKind.Spawned, "other-card", "token-spike");

        Assert.That(Log(), Does.Contain("Link Spawned other-card -> token-spike refused: token-spike was already spawned by auth-research."));
    }

    [Test]
    public void Remove_LogsTheLinkRemoved()
    {
        var link = _book.Add(LinkKind.Spawned, "auth-research", "token-spike").Link!;

        _book.Remove(link.Id);

        Assert.That(Log(), Does.Contain($"Link {link.Id} removed"));
    }

    [Test]
    public void Satisfy_LogsARefusalWithItsReason()
    {
        var link = _book.Add(LinkKind.Spawned, "auth-research", "token-spike").Link!;

        _book.Satisfy(link.Id, "read it");

        Assert.That(Log(), Does.Contain($"Link {link.Id} not satisfied: Only a link a card waits on can be satisfied."));
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
