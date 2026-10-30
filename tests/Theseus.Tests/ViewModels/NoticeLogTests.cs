using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>The status bar shows one notice; the log keeps the recent ones so none is lost unread.</summary>
[TestFixture]
public class NoticeLogTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void Add_KeepsTheNewestFirstWithItsTime()
    {
        var log = new NoticeLog(() => Nine);

        log.Add("Archived Test 1.");
        log.Add("Restored Test 1.");

        Assert.That(log.Entries.Select(entry => entry.Text), Is.EqualTo(new[] { "Restored Test 1.", "Archived Test 1." }));
        Assert.That(log.Entries[0].Display, Is.EqualTo($"{Nine:HH:mm}  Restored Test 1."));
    }

    [Test]
    public void Add_IgnoresBlanksAndAnImmediateRepeat()
    {
        var log = new NoticeLog(() => Nine);

        log.Add("Closing 2 sessions.");
        log.Add("Closing 2 sessions.");
        log.Add(" ");

        Assert.That(log.Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public void Add_DropsTheOldestPastCapacity()
    {
        var log = new NoticeLog(() => Nine);

        for (var index = 0; index < NoticeLog.Capacity + 5; index++)
        {
            log.Add($"Notice {index}");
        }

        Assert.That(log.Entries, Has.Count.EqualTo(NoticeLog.Capacity));
        Assert.That(log.Entries[^1].Text, Is.EqualTo("Notice 5"));
    }
}
