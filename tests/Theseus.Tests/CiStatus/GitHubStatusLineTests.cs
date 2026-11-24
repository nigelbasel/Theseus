using Theseus.Core.CiStatus;

namespace Theseus.Tests.CiStatus;

[TestFixture]
public class GitHubStatusLineTests
{
    private static readonly GitHubRepoRef Repo = new("owner", "repo");
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 22, 0, 0, TimeSpan.Zero);

    [Test]
    public void WithNoGitHubRepositoryPolled_NothingIsShown()
    {
        Assert.That(GitHubStatusLine.For(new GitHubStatusBook(), 0, Now, TimeZoneInfo.Utc), Is.EqualTo(GitHubStatusLine.Hidden));
    }

    [Test]
    public void BeforeAnyAnswer_ItSaysNotReadYet_AndThatTheBudgetIsNotKnown()
    {
        var line = GitHubStatusLine.For(new GitHubStatusBook(), 1, Now, TimeZoneInfo.Utc);

        Assert.That(line.Text, Is.EqualTo("GitHub not read yet"));
        Assert.That(line.Tooltip, Does.Contain("not known until GitHub next answers"));
    }

    [Test]
    public void AfterARead_ItSaysWhen_AndTheTooltipGivesTheCallsLeftAndTheSchedule()
    {
        var book = new GitHubStatusBook();
        book.Record(Repo, new GitHubRead(new RepoReading(RepoReadingKind.Read, Now, [], []), new GitHubBudget(60, 46, Now.AddMinutes(13))), Now);

        var line = GitHubStatusLine.For(book, 1, Now, TimeZoneInfo.Utc);

        Assert.That(line.Text, Is.EqualTo("GitHub read 22:00"));
        Assert.That(line.IsCaution, Is.False);
        Assert.That(line.Tooltip, Does.Contain("46 of 60 calls left this hour; they reset at 22:13."));
        Assert.That(line.Tooltip, Does.Contain("1 repository polled every 5 minutes, two calls each."));
    }

    [Test]
    public void WhileTheLimitIsUsedUp_ItSaysUntilWhen_InTheCautionColour()
    {
        var book = new GitHubStatusBook();
        var reset = Now.AddMinutes(13);
        book.Record(Repo, new GitHubRead(RepoReading.Unknown(RepoReadingKind.Limited, Now) with { LimitResetsAt = reset },
            new GitHubBudget(60, 0, reset)), Now);

        var line = GitHubStatusLine.For(book, 1, Now, TimeZoneInfo.Utc);

        Assert.That(line.Text, Is.EqualTo("⚠ GitHub limit until 22:13"));
        Assert.That(line.IsCaution, Is.True);
    }
}
