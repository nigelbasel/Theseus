using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>
/// Theseus creates no handover file inside a git working tree that can be pushed (handoff-names review S5), and
/// moves them freely in a repository with no remote (Nigel, 2026-11-20).
/// </summary>
[TestFixture]
public class GitWorkingTreeTests
{
    private const string WithRemote = "[core]\n\tbare = false\n[remote \"origin\"]\n\turl = https://example.com/r.git\n";
    private const string NoRemote = "[core]\n\tbare = false\n";

    private string _root = string.Empty;

    [SetUp]
    public void SetUp() => _root = Directory.CreateTempSubdirectory("git-tree-").FullName;

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void AFolderBelowARepositoryWithARemote_IsPublishable()
    {
        WriteRepository(_root, WithRemote);
        var notes = Directory.CreateDirectory(Path.Combine(_root, "docs", "notes")).FullName;

        Assert.That(GitWorkingTree.ContainsPublishable(notes), Is.True);
    }

    [Test]
    public void ARepositoryWithNoRemote_IsNotPublishable()
    {
        WriteRepository(_root, NoRemote);

        Assert.That(GitWorkingTree.ContainsPublishable(_root), Is.False);
    }

    [Test]
    public void ARepositoryWithNoConfig_CountsAsPublishable()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        Assert.That(GitWorkingTree.ContainsPublishable(_root), Is.True);
    }

    [Test]
    public void AWorktree_IsJudgedByItsRepositorysSharedConfig()
    {
        var repository = Directory.CreateDirectory(Path.Combine(_root, "repo")).FullName;
        WriteRepository(repository, WithRemote);
        var ownGit = Directory.CreateDirectory(Path.Combine(repository, ".git", "worktrees", "wt")).FullName;
        File.WriteAllText(Path.Combine(ownGit, "commondir"), "../..");
        var worktree = Directory.CreateDirectory(Path.Combine(_root, "wt")).FullName;
        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {ownGit}");

        Assert.That(GitWorkingTree.ContainsPublishable(worktree), Is.True);

        WriteRepository(repository, NoRemote);

        Assert.That(GitWorkingTree.ContainsPublishable(worktree), Is.False);
    }

    [Test]
    public void AWorktreeWhoseGitFolderIsMissing_CountsAsPublishable()
    {
        File.WriteAllText(Path.Combine(_root, ".git"), "gitdir: elsewhere");

        Assert.That(GitWorkingTree.ContainsPublishable(_root), Is.True);
    }

    [Test]
    public void AFolderWithNoGitAbove_IsNotPublishable()
    {
        Assert.That(GitWorkingTree.ContainsPublishable(_root), Is.False, "the temp folder is not inside a repository");
    }

    private static void WriteRepository(string folder, string config)
    {
        var git = Directory.CreateDirectory(Path.Combine(folder, ".git")).FullName;
        File.WriteAllText(Path.Combine(git, "config"), config);
    }
}
