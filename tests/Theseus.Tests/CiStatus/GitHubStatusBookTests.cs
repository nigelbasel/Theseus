using Theseus.Core.CiStatus;

namespace Theseus.Tests.CiStatus;

[TestFixture]
public class GitHubStatusBookTests
{
    private static readonly GitHubRepoRef One = new("owner", "one");
    private static readonly GitHubRepoRef Two = new("owner", "two");
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 22, 0, 0, TimeSpan.Zero);

    [TestCase(1, 5)]
    [TestCase(2, 5)]
    [TestCase(3, 7.5)]
    [TestCase(4, 10)]
    public void Interval_IsFiveMinutes_OrLongerAsRepositoriesGrow(int repositories, double minutes)
    {
        Assert.That(GitHubStatusBook.Interval(repositories), Is.EqualTo(TimeSpan.FromMinutes(minutes)));
    }

    [TestCase(1)]
    [TestCase(4)]
    [TestCase(10)]
    [TestCase(40)]
    public void ScheduledPolling_MakesAtMost48CallsInAnyHour_HoweverManyRepositories(int repositories)
    {
        var book = new GitHubStatusBook();
        var repos = Enumerable.Range(0, repositories).Select(index => new GitHubRepoRef("owner", $"r{index}")).ToList();
        var reads = new List<DateTimeOffset>();

        // A timer ticking every minute for three hours, every read answered with plenty left.
        for (var minute = 0; minute < 180; minute++)
        {
            var now = Now.AddMinutes(minute);

            foreach (var repo in book.Due(repos, now, refresh: false))
            {
                book.Record(repo, Read(now, remaining: 50), now);
                reads.Add(now);
            }
        }

        var worstHour = reads.Max(start => reads.Count(at => at >= start && at < start.AddHours(1)));

        Assert.That(worstHour * GitHubStatusBook.CallsPerRead, Is.LessThanOrEqualTo(GitHubStatusBook.PollingCallsPerHour));
    }

    [Test]
    public void OnSchedule_OneRepositoryIsReadAtATime_TheOneWaitingLongest()
    {
        var book = new GitHubStatusBook();

        Assert.That(book.Due([One, Two, new GitHubRepoRef("Owner", "One")], Now, refresh: false), Is.EqualTo(new[] { One }));
        book.Record(One, Read(Now, remaining: 50), Now);

        Assert.That(book.Due([One, Two], Now.AddMinutes(1), refresh: false), Is.Empty);
        Assert.That(book.Due([One, Two], Now.Add(GitHubStatusBook.Spacing), refresh: false), Is.EqualTo(new[] { Two }));
    }

    [Test]
    public void ARepositoryRead_IsNotDueAgain_UntilFiveMinutesHavePassed()
    {
        var book = new GitHubStatusBook();
        book.Record(One, Read(Now, remaining: 50), Now);

        Assert.That(book.Due([One], Now.AddMinutes(4), refresh: false), Is.Empty);
        Assert.That(book.Due([One], Now.AddMinutes(5), refresh: false), Is.EqualTo(new[] { One }));
    }

    [Test]
    public void Refresh_ReadsEveryRepositoryAtOnce_WhileTheCallsLeftAllowItAboveTheReserve()
    {
        var book = new GitHubStatusBook();
        book.Record(One, Read(Now, remaining: GitHubStatusBook.Reserve + 4), Now);

        Assert.That(book.Due([One, Two], Now.AddMinutes(1), refresh: true), Is.EquivalentTo(new[] { One, Two }));
    }

    [Test]
    public void Refresh_ReadsOnlyAsManyAsKeepTheReserve()
    {
        var book = new GitHubStatusBook();
        book.Record(One, Read(Now, remaining: GitHubStatusBook.Reserve + 3), Now);

        Assert.That(book.Due([One, Two], Now.AddMinutes(1), refresh: true), Is.EqualTo(new[] { Two }));
    }

    [Test]
    public void Refresh_ReadsNothing_OnceTheReserveIsReached()
    {
        var book = new GitHubStatusBook();
        book.Record(One, Read(Now, remaining: GitHubStatusBook.Reserve + 1), Now);

        Assert.That(book.Due([One], Now.AddMinutes(1), refresh: true), Is.Empty);
    }

    [Test]
    public void NothingIsDue_WhileTheLimitIsUsedUp_RefreshIncluded_UntilItResets()
    {
        var book = new GitHubStatusBook();
        var reset = Now.AddMinutes(20);
        book.Record(One, new GitHubRead(RepoReading.Unknown(RepoReadingKind.Limited, Now) with { LimitResetsAt = reset },
            new GitHubBudget(60, 0, reset)), Now);

        Assert.That(book.LimitedUntil(Now), Is.EqualTo(reset));
        Assert.That(book.Due([One, Two], Now.AddMinutes(10), refresh: true), Is.Empty);
        Assert.That(book.Due([One, Two], Now.AddMinutes(10), refresh: false), Is.Empty);
        Assert.That(book.LimitedUntil(reset), Is.Null);
        Assert.That(book.Due([One, Two], reset, refresh: false), Is.EqualTo(new[] { Two }));
    }

    [Test]
    public void FewerCallsLeftThanARead_CountsAsTheLimit()
    {
        var book = new GitHubStatusBook();
        book.Record(One, Read(Now, remaining: 1), Now);

        Assert.That(book.LimitedUntil(Now), Is.Not.Null);
        Assert.That(book.Due([Two], Now.AddMinutes(5), refresh: false), Is.Empty);
    }

    [Test]
    public void LastReadAt_MovesOnlyWithASuccessfulRead()
    {
        var book = new GitHubStatusBook();
        book.Record(One, Read(Now, remaining: 50), Now);
        book.Record(Two, new GitHubRead(RepoReading.Unknown(RepoReadingKind.Failed, Now.AddMinutes(1)), null), Now.AddMinutes(1));

        Assert.That(book.LastReadAt, Is.EqualTo(Now));
        Assert.That(book.Reading(Two)!.Kind, Is.EqualTo(RepoReadingKind.Failed));
    }

    private static GitHubRead Read(DateTimeOffset at, int remaining) =>
        new(new RepoReading(RepoReadingKind.Read, at, [], []), new GitHubBudget(60, remaining, at.AddMinutes(40)));
}
