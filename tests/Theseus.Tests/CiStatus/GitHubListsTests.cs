using Theseus.Core.CiStatus;

namespace Theseus.Tests.CiStatus;

[TestFixture]
public class GitHubListsTests
{
    [Test]
    public void PullRequests_ReadsNumberTitleBranchDraftAndPage()
    {
        var pulls = GitHubLists.PullRequests(GitHubJson.Pulls(GitHubJson.Pull(41, "user/feature", draft: true, title: "Status on cards")));

        Assert.That(pulls, Has.Count.EqualTo(1));
        Assert.That(pulls[0], Is.EqualTo(new OpenPullRequest(41, "Status on cards", "user/feature", "owner/repo", true,
            "https://github.com/owner/repo/pull/41")));
    }

    [Test]
    public void PullRequests_SkipsAnEntryWithoutAHeadBranch()
    {
        var pulls = GitHubLists.PullRequests("""[{"number":3,"html_url":"https://github.com/o/r/pull/3"},"junk"]""");

        Assert.That(pulls, Is.Empty);
    }

    [Test]
    public void Runs_ReadsBranchStateAndTimes()
    {
        var runs = GitHubLists.Runs(GitHubJson.Runs(GitHubJson.Run(7, "main", "completed", "success", "2026-11-24T21:38:00Z", "2026-11-24T21:43:01Z")));

        Assert.That(runs, Has.Count.EqualTo(1));
        Assert.That(runs[0].Branch, Is.EqualTo("main"));
        Assert.That(runs[0].Conclusion, Is.EqualTo("success"));
        Assert.That(runs[0].UpdatedAt, Is.EqualTo(new DateTimeOffset(2026, 11, 24, 21, 43, 1, TimeSpan.Zero)));
        Assert.That(runs[0].Url, Is.EqualTo("https://github.com/owner/repo/actions/runs/7"));
    }

    [Test]
    public void Runs_IsEmpty_ForAnAnswerThatIsNotARunList()
    {
        Assert.That(GitHubLists.Runs("[]"), Is.Empty);
    }
}
