using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

/// <summary>
/// Which repositories a card covers, found from its extra directories (card-branches spec AC 1 and 2): a clone, a
/// worktree, or each repository directly under a feature folder, each counted once.
/// </summary>
[TestFixture]
public class CardReposTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-card-repos-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public void AClone_IsOneRepo()
    {
        var clone = Clone("api", "main");

        Assert.That(CardRepos.Find([clone]), Is.EqualTo(new[] { clone }));
    }

    [Test]
    public void AWorktree_IsOneRepo_ReadThroughItsGitFile()
    {
        var worktree = Worktree(Path.Combine("feature", "api"), "user/feature");

        var repos = CardRepos.Read([worktree]);

        Assert.That(repos.Select(repo => repo.Path), Is.EqualTo(new[] { worktree }));
        Assert.That(repos[0].Branch, Is.EqualTo("user/feature"));
        Assert.That(repos[0].Name, Is.EqualTo("api"));
    }

    [Test]
    public void AFolderInsideARepo_CountsAsThatRepo()
    {
        var clone = Clone("api", "main");
        var inside = Directory.CreateDirectory(Path.Combine(clone, "src", "deep")).FullName;

        Assert.That(CardRepos.Find([inside]), Is.EqualTo(new[] { clone }));
    }

    [Test]
    public void AFeatureFolder_CountsAsEachRepoDirectlyUnderIt()
    {
        var api = Worktree(Path.Combine("feature", "api"), "user/feature");
        var ui = Worktree(Path.Combine("feature", "ui"), "user/feature");
        Directory.CreateDirectory(Path.Combine(_root, "feature", "notes"));

        Assert.That(CardRepos.Find([Path.Combine(_root, "feature")]), Is.EquivalentTo(new[] { api, ui }));
    }

    [Test]
    public void TheSameRepoReachedTwice_IsCountedOnce_InTheOrderFirstReached()
    {
        var api = Clone("api", "main");
        var ui = Clone("ui", "main");

        Assert.That(CardRepos.Find([ui, Path.Combine(api, "."), api, ui]), Is.EqualTo(new[] { ui, api }));
    }

    [Test]
    public void AMissingFolder_OrOneWithNoRepos_ContributesNothing()
    {
        var empty = Directory.CreateDirectory(Path.Combine(_root, "empty")).FullName;

        Assert.That(CardRepos.Find([Path.Combine(_root, "not-there"), empty, "", "   "]), Is.Empty);
    }

    [Test]
    public void NoExtraDirectories_IsNoRepos()
    {
        Assert.That(CardRepos.Find([]), Is.Empty);
    }

    private string Clone(string name, string branch)
    {
        var path = Directory.CreateDirectory(Path.Combine(_root, name)).FullName;
        var git = Directory.CreateDirectory(Path.Combine(path, ".git")).FullName;
        File.WriteAllText(Path.Combine(git, "HEAD"), $"ref: refs/heads/{branch}\n");

        return path;
    }

    private string Worktree(string relative, string branch)
    {
        var path = Directory.CreateDirectory(Path.Combine(_root, relative)).FullName;
        var gitDirectory = Directory.CreateDirectory(Path.Combine(_root, "gitdirs", Guid.NewGuid().ToString("N"))).FullName;
        File.WriteAllText(Path.Combine(gitDirectory, "HEAD"), $"ref: refs/heads/{branch}\n");
        File.WriteAllText(Path.Combine(path, ".git"), $"gitdir: {gitDirectory}\n");

        return path;
    }
}
