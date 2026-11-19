using System.Diagnostics;
using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

/// <summary>
/// card-branches, implement pass 2 (2026-11-19): what verify part 1 found. A card with no repos shows what it showed
/// before (the tooltip was the bare branch: WPF ignores StringFormat on a ToolTip); a network folder does not stall
/// the board; a repo is counted once by its real location; one origin written two ways is one origin.
/// </summary>
[TestFixture]
public class CardBranchesFixTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-card-fix-{Guid.NewGuid():N}");
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
    public void ACardWithNoRepos_HasTheBareBranchAsItsTooltip_AsTheBoardShowedBefore()
    {
        var working = Clone("notes", "main");

        Assert.That(BranchLines.For(working, []).Tooltip, Is.EqualTo("main"));
    }

    [Test]
    public void ANetworkFolder_IsSkippedWithoutBeingTouched()
    {
        var watch = Stopwatch.StartNew();

        var repos = CardRepos.Find([@"\\theseus-no-such-host.invalid\share\feature"]);

        Assert.That(repos, Is.Empty);
        Assert.That(watch.ElapsedMilliseconds, Is.LessThan(200), "an unreachable server must not stall a refresh");
    }

    [Test]
    public void AFeatureFolder_ListsItsReposByName()
    {
        var feature = Path.Combine(_root, "feature");
        var zeta = Clone(Path.Combine("feature", "zeta"), "main");
        var alpha = Clone(Path.Combine("feature", "alpha"), "main");

        Assert.That(CardRepos.Find([feature]), Is.EqualTo(new[] { alpha, zeta }));
    }

    [Test]
    public void AWorktreeWithARelativeGitDirectory_IsRead()
    {
        var worktree = Directory.CreateDirectory(Path.Combine(_root, "feature", "api")).FullName;
        var gitDirectory = Directory.CreateDirectory(Path.Combine(_root, "store", "worktrees", "api")).FullName;
        File.WriteAllText(Path.Combine(gitDirectory, "HEAD"), "ref: refs/heads/user/feature\n");
        File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: ../../store/worktrees/api\n");

        Assert.That(CardRepos.Read([worktree]).Single().Branch, Is.EqualTo("user/feature"));
    }

    [Test]
    public void AWorkingDirectoryThatIsARepo_IsNotShown_WhenTheCardDeclaresOtherRepos()
    {
        var working = Clone("notes", "main");
        var api = Clone("api", "user/feature");

        var line = BranchLines.For(working, CardRepos.Read([api]));

        Assert.That(line.Text, Is.EqualTo("on user/feature"));
        Assert.That(line.Tooltip, Does.Not.Contain("main"));
    }

    [Test]
    public void ALinkToARepo_IsTheSameRepo()
    {
        var api = Clone("api", "main");
        var link = Path.Combine(_root, "api-link");

        try
        {
            Directory.CreateSymbolicLink(link, api);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Assert.Ignore("This machine does not allow creating a directory link here.");
        }

        Assert.That(CardRepos.Find([api, link]), Has.Count.EqualTo(1));
    }

    [TestCase("https://github.com/owner/app.git", "https://github.com/owner/app")]
    [TestCase("https://github.com/Owner/App/", "https://github.com/owner/app")]
    [TestCase("git@github.com:owner/app.git", "https://github.com/owner/app")]
    [TestCase("ssh://git@github.com/owner/app.git", "https://github.com/owner/app")]
    public void OneOriginWrittenTwoWays_IsOneOrigin(string first, string second)
    {
        var api = Clone("api", "main");
        var ui = Clone("ui", "main");
        var remotes = new Remotes { [api] = first, [ui] = second };

        var link = new PullRequestResolver(remotes).Resolve(Card([api, ui], "Review PR 42.")).Single();

        Assert.That(link.Url, Is.Not.Null, "the two repos share an origin, so the number can only be in it");
        Assert.That(link.Note, Is.Null);
    }

    private string Clone(string relative, string branch)
    {
        var path = Directory.CreateDirectory(Path.Combine(_root, relative)).FullName;
        var git = Directory.CreateDirectory(Path.Combine(path, ".git")).FullName;
        File.WriteAllText(Path.Combine(git, "HEAD"), $"ref: refs/heads/{branch}\n");

        return path;
    }

    private Workstream Card(IReadOnlyList<string> addDirectories, string prompt) =>
        new("feature", "Feature", "#208DD7", false, _root, addDirectories, prompt, 0, "New", false, false,
            false, null, [], null, DateTimeOffset.UnixEpoch);

    private sealed class Remotes : Dictionary<string, string>, IGitRemoteReader
    {
        public Remotes()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public string? ReadOriginUrl(string? directory) =>
            directory is not null && TryGetValue(directory, out var url) ? url : null;
    }
}
