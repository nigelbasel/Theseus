using Theseus.Core.CiStatus;

namespace Theseus.Tests.CiStatus;

[TestFixture]
public class GitHubRepoRefTests
{
    [TestCase("https://github.com/owner/repo.git")]
    [TestCase("https://github.com/owner/repo")]
    [TestCase("https://github.com/owner/repo/")]
    [TestCase("git@github.com:owner/repo.git")]
    [TestCase("ssh://git@github.com/owner/repo.git")]
    [TestCase("https://user@GitHub.com/owner/repo")]
    public void FromRemote_ReadsOwnerAndName_HoweverTheRemoteIsWritten(string remote)
    {
        var repo = GitHubRepoRef.FromRemote(remote);

        Assert.That(repo, Is.EqualTo(new GitHubRepoRef("owner", "repo")));
    }

    [TestCase("https://dev.example.com/org/Project/_git/repo")]
    [TestCase("https://gitlab.com/owner/repo.git")]
    [TestCase("https://github.com/owner")]
    [TestCase("https://github.com/owner/repo/extra")]
    [TestCase("")]
    [TestCase(null)]
    public void FromRemote_IsNull_ForAnythingButAGitHubRepository(string? remote)
    {
        Assert.That(GitHubRepoRef.FromRemote(remote), Is.Null);
    }

    [Test]
    public void Key_IsTheSame_WhateverTheCase()
    {
        Assert.That(new GitHubRepoRef("Owner", "Repo").Key, Is.EqualTo(new GitHubRepoRef("owner", "repo").Key));
    }

    [TestCase("https://dev.example.com/org/_git/repo", "dev.example.com")]
    [TestCase("git@host.example:team/repo.git", "host.example")]
    [TestCase("ssh://git@host.example:2222/team/repo", "host.example")]
    public void RemoteHost_NamesTheHost(string remote, string host)
    {
        Assert.That(RemoteHost.Of(remote), Is.EqualTo(host));
    }
}
