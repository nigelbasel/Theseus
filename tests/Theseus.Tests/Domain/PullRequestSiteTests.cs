using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class PullRequestSiteTests
{
    private const string AzureDevOps = "https://tfs.example.com/ORG/Project/_git/Some.Repository";
    private const string GitHub = "https://github.com/owner/repo.git";

    [Test]
    public void Url_AddressesAnAzureDevOpsPullRequest()
    {
        Assert.That(PullRequestSite.Url(AzureDevOps, 20769),
            Is.EqualTo("https://tfs.example.com/ORG/Project/_git/Some.Repository/pullrequest/20769"));
    }

    [Test]
    public void Url_AddressesAGitHubPullRequestAndDropsTheGitSuffix()
    {
        Assert.That(PullRequestSite.Url(GitHub, 42), Is.EqualTo("https://github.com/owner/repo/pull/42"));
    }

    [Test]
    public void Url_HandlesAnSshStyleGitHubRemote()
    {
        Assert.That(PullRequestSite.Url("git@github.com:owner/repo.git", 42),
            Is.EqualTo("https://github.com/owner/repo/pull/42"));
    }

    [Test]
    public void Url_StripsCredentialsRatherThanPuttingThemInALinkThatGetsOpened()
    {
        var url = PullRequestSite.Url("https://someone:secret@tfs.example.com/ORG/P/_git/R", 7);

        Assert.That(url, Is.EqualTo("https://tfs.example.com/ORG/P/_git/R/pullrequest/7"));
        Assert.That(url, Does.Not.Contain("secret"));
    }

    [Test]
    public void Url_ToleratesATrailingSlash()
    {
        Assert.That(PullRequestSite.Url(AzureDevOps + "/", 20769), Does.EndWith("/pullrequest/20769"));
    }

    [Test]
    public void Url_GivesNothingForAHostItCannotAddress()
    {
        Assert.That(PullRequestSite.Url("https://gitlab.example.com/group/repo.git", 5), Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not a url")]
    [TestCase("file:///c:/dev/repo")]
    public void Url_GivesNothingWhenThereIsNoUsableRemote(string? remote)
    {
        Assert.That(PullRequestSite.Url(remote, 20769), Is.Null);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Url_RefusesANumberThatIsNotAPullRequest(int number)
    {
        Assert.That(PullRequestSite.Url(AzureDevOps, number), Is.Null);
    }
}
