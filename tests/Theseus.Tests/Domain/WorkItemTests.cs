using Theseus.App.ViewModels;
using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

/// <summary>
/// A card's Jira issues and pull requests, with the titles sessions report, and its branch (Nigel,
/// 2026-10-30: drop-downs rather than a long row of chips, with the title as well as the number).
/// </summary>
[TestFixture]
public class WorkItemTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    private string _path = string.Empty;

    [SetUp]
    public void SetUp() => _path = Path.Combine(Path.GetTempPath(), $"theseus-work-items-{Guid.NewGuid():N}.json");

    [TearDown]
    public void TearDown() => File.Delete(_path);

    [Test]
    public void Record_KeepsTitlesPerCardAndAcrossARestart()
    {
        var book = new WorkItemBook(_path);

        book.Record(new WorkItem("auth", WorkItemKind.Jira, "ndc-680", "Mandatory 2FA", null, Nine));
        book.Record(new WorkItem("auth", WorkItemKind.PullRequest, "#22174", "Enforce 2FA at sign-in", null, Nine));
        book.Record(new WorkItem("other", WorkItemKind.Jira, "NDC-1", "Elsewhere", null, Nine));

        var reopened = new WorkItemBook(_path).For("auth");

        Assert.That(reopened.Select(item => (item.Kind, item.Id, item.Title)), Is.EqualTo(new[]
        {
            (WorkItemKind.Jira, "NDC-680", (string?)"Mandatory 2FA"),
            (WorkItemKind.PullRequest, "22174", (string?)"Enforce 2FA at sign-in"),
        }));
    }

    [Test]
    public void Record_AgainUpdatesTheTitleAndKeepsOneEntry()
    {
        var book = new WorkItemBook(_path);

        book.Record(new WorkItem("auth", WorkItemKind.Jira, "NDC-680", "Old title", null, Nine));
        book.Record(new WorkItem("auth", WorkItemKind.Jira, "NDC-680", "New title", null, Nine));

        Assert.That(book.For("auth").Single().Title, Is.EqualTo("New title"));
    }

    [Test]
    public void Record_RefusesAPullRequestThatIsNotANumber()
    {
        var refusal = new WorkItemBook(_path).Record(new WorkItem("auth", WorkItemKind.PullRequest, "main", "x", null, Nine));

        Assert.That(refusal, Does.Contain("number"));
    }

    [Test]
    public void JiraRows_GiveEveryKeyWithItsTitleAndAddress()
    {
        var rows = CardItemRows.Jira(["NDC-680", "NDC-681"],
            [new WorkItem("auth", WorkItemKind.Jira, "NDC-680", "Mandatory 2FA", null, Nine),
             new WorkItem("auth", WorkItemKind.Jira, "NDC-900", "Raised by the session", null, Nine)],
            "https://example.atlassian.net");

        Assert.Multiple(() =>
        {
            Assert.That(rows.Select(row => row.Display), Is.EqualTo(new[] { "NDC-680   Mandatory 2FA", "NDC-681", "NDC-900   Raised by the session" }));
            Assert.That(rows[1].Url, Is.EqualTo("https://example.atlassian.net/browse/NDC-681"));
        });
    }

    [Test]
    public void PullRequestRows_AddTitlesToTheMentionedOnes()
    {
        var rows = CardItemRows.PullRequests([new PullRequestLink(22174, "https://tfs/pr/22174")],
            [new WorkItem("auth", WorkItemKind.PullRequest, "22174", "Enforce 2FA", null, Nine)]);

        Assert.That(rows.Single().Display, Is.EqualTo("PR 22174   Enforce 2FA"));
        Assert.That(rows.Single().Url, Is.EqualTo("https://tfs/pr/22174"));
    }

    [TestCase("ref: refs/heads/nigel/gdpr\n", "nigel/gdpr")]
    [TestCase("ref: refs/heads/main", "main")]
    [TestCase("0123456789abcdef0123456789abcdef01234567\n", "detached at 0123456")]
    public void Branch_IsReadFromHead(string head, string branch)
    {
        Assert.That(GitBranchReader.Parse(head), Is.EqualTo(branch));
    }

    [Test]
    public void Branch_OfAWorktreeIsItsOwn()
    {
        var root = Directory.CreateTempSubdirectory("theseus-branch-").FullName;

        try
        {
            var gitDirectory = Directory.CreateDirectory(Path.Combine(root, "main", ".git", "worktrees", "gdpr")).FullName;
            File.WriteAllText(Path.Combine(gitDirectory, "HEAD"), "ref: refs/heads/nigel/gdpr\n");
            var worktree = Directory.CreateDirectory(Path.Combine(root, "features", "gdpr", "repo", "src")).FullName;
            File.WriteAllText(Path.Combine(root, "features", "gdpr", "repo", ".git"), $"gitdir: {gitDirectory}\n");

            Assert.That(GitBranchReader.Read(worktree), Is.EqualTo("nigel/gdpr"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
