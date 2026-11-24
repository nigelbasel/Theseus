using Theseus.Core.Domain;
using Theseus.Core.Worktrees;

namespace Theseus.Tests.Worktrees;

/// <summary>
/// The layout rule (worktree-per-repo AC 5 to 7): a clone found at the path a reference spells, or by the tail of its
/// path or its origin URL; none and several told apart; a worktree never taken for a clone.
/// </summary>
[TestFixture]
public class CloneLocatorTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-clone-locator-{Guid.NewGuid():N}");
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
    public void APathReference_FindsTheCloneItSpells()
    {
        var clone = Clone("ado", "Team", "Service");

        Assert.That(new CloneLocator(_root, new Remotes()).Find("ado/Team/Service").ClonePath, Is.EqualTo(clone));
    }

    [Test]
    public void AReferenceWithAnotherHostSpelling_IsFoundByItsTail()
    {
        var clone = Clone("github", "owner", "app");

        Assert.That(new CloneLocator(_root, new Remotes()).Find("github.com/owner/app").ClonePath, Is.EqualTo(clone));
    }

    [Test]
    public void AShortName_FindsTheOneCloneOfThatName()
    {
        var clone = Clone("github", "owner", "app");
        Clone("github", "owner", "app-holdout");

        Assert.That(new CloneLocator(_root, new Remotes()).Find("app").ClonePath, Is.EqualTo(clone));
    }

    [Test]
    public void AClone_IsFoundByItsOriginUrl_WhereverItSits()
    {
        var clone = Clone("other", "renamed-locally");
        var remotes = new Remotes { [clone] = "https://host.example/Team/_git/My%20Repo" };

        Assert.That(new CloneLocator(_root, remotes).Find("ado/Team/My Repo").ClonePath, Is.EqualTo(clone));
    }

    [Test]
    public void NoMatch_GivesNoCloneAndNoCandidates()
    {
        Clone("github", "owner", "app");

        var match = new CloneLocator(_root, new Remotes()).Find("github/owner/missing");

        Assert.That(match.ClonePath, Is.Null);
        Assert.That(match.Candidates, Is.Empty);
    }

    [Test]
    public void SeveralMatches_GiveNoClone_ButNameEachCandidate()
    {
        var one = Clone("github", "first", "app");
        var two = Clone("ado", "second", "app");

        var match = new CloneLocator(_root, new Remotes()).Find("app");

        Assert.That(match.ClonePath, Is.Null);
        Assert.That(match.Candidates, Is.EquivalentTo(new[] { one, two }));
    }

    [Test]
    public void AWorktree_IsNeverTakenForAClone()
    {
        var worktree = Path.Combine(_root, "work", "feature", "app");
        Directory.CreateDirectory(worktree);
        File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: elsewhere");

        Assert.That(new CloneLocator(_root, new Remotes()).Find("app").Candidates, Is.Empty);
    }

    private string Clone(params string[] segments)
    {
        var folder = Path.Combine([_root, .. segments]);
        Directory.CreateDirectory(Path.Combine(folder, ".git"));

        return Path.GetFullPath(folder);
    }

    private sealed class Remotes : Dictionary<string, string>, IGitRemoteReader
    {
        public Remotes()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public string? ReadOriginUrl(string? directory) => directory is not null && TryGetValue(directory, out var url) ? url : null;
    }
}
