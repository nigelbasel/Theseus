using Theseus.Core.Abstractions;
using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// The start requests (sessions-run-the-project items 12 to 15): one waiting per card, a repeat updating it, the owner's
/// answer or a lapse leaving pending, and every request surviving a restart.
/// </summary>
[TestFixture]
public class StartRequestBookTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);
    private string _path = string.Empty;

    [SetUp]
    public void SetUp() => _path = Path.Combine(Path.GetTempPath(), $"theseus-start-requests-{Guid.NewGuid():N}.json");

    [TearDown]
    public void TearDown() => File.Delete(_path);

    [Test]
    public void Ask_FilesOneRequestPerCard_AndARepeatUpdatesIt()
    {
        var book = Book();

        var (first, firstRepeat) = book.Ask("paused-card", "lead", "go on", "the spec is accepted");
        var (second, secondRepeat) = book.Ask("paused-card", "other", null, "now it is urgent");

        Assert.Multiple(() =>
        {
            Assert.That(firstRepeat, Is.False);
            Assert.That(secondRepeat, Is.True);
            Assert.That(second.Id, Is.EqualTo(first.Id));
            Assert.That((second.RequesterKey, second.Instruction, second.Reason), Is.EqualTo(("lead", "go on", "now it is urgent")),
                "the first asker keeps it, and is the one told (review of PR 2, finding 5)");
            Assert.That(book.Pending, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Decide_LeavesPending_Once()
    {
        var book = Book();
        var (request, _) = book.Ask("paused-card", "lead", null, null);

        var refused = book.Decide(request.Id, StartRequestStatus.Refused, "  not yet  ");

        Assert.Multiple(() =>
        {
            Assert.That(refused!.Status, Is.EqualTo(StartRequestStatus.Refused));
            Assert.That(refused.Note, Is.EqualTo("not yet"));
            Assert.That(refused.DecidedUtc, Is.EqualTo(Nine));
            Assert.That(book.Pending, Is.Empty);
            Assert.That(book.Decide(request.Id, StartRequestStatus.Allowed, null), Is.Null, "already decided");
            Assert.Throws<ArgumentOutOfRangeException>(() => book.Decide(request.Id, StartRequestStatus.Pending, null));
        });
    }

    [Test]
    public void AfterADecision_TheCardMayBeAskedForAgain()
    {
        var book = Book();
        var (first, _) = book.Ask("paused-card", "lead", null, null);
        book.Decide(first.Id, StartRequestStatus.Refused, null);

        var (second, repeat) = book.Ask("paused-card", "lead", null, null);

        Assert.That((repeat, second.Id == first.Id), Is.EqualTo((false, false)));
    }

    [Test]
    public void Requests_SurviveARestart()
    {
        var (request, _) = Book().Ask("paused-card", "lead", "go on", "because");

        var reopened = Book();

        Assert.That(reopened.Find(request.Id), Is.EqualTo(request));
        Assert.That(reopened.Pending.Single().Id, Is.EqualTo(request.Id));
    }

    private StartRequestBook Book() => new(new StartRequestStore(_path), new FixedClock(Nine));

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
