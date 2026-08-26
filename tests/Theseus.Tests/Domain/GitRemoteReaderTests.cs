using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class GitRemoteReaderTests
{
    private const string Remote = "https://tfs.example.com/ORG/Project/_git/Some.Repository";

    private static readonly string[] Config =
    [
        "[core]",
        "\trepositoryformatversion = 0",
        "[remote \"origin\"]",
        $"\turl = {Remote}",
        "\tfetch = +refs/heads/*:refs/remotes/origin/*",
        "[branch \"main\"]",
        "\tremote = origin",
    ];

    private string _root = string.Empty;
    private GitRemoteReader _reader = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-git-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _reader = new GitRemoteReader();
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void ReadOriginUrl_ReadsAnOrdinaryRepository()
    {
        var repository = Repository("main");

        Assert.That(_reader.ReadOriginUrl(repository), Is.EqualTo(Remote));
    }

    [Test]
    public void ReadOriginUrl_WalksUpFromASubdirectory()
    {
        var repository = Repository("main");
        var nested = Path.Combine(repository, "src", "Some.Project");
        Directory.CreateDirectory(nested);

        Assert.That(_reader.ReadOriginUrl(nested), Is.EqualTo(Remote));
    }

    [Test]
    public void ReadOriginUrl_FollowsAWorktreeToTheRepositoryHoldingItsConfiguration()
    {
        var repository = Repository("main");
        var worktree = Worktree(repository, "feature");

        Assert.That(_reader.ReadOriginUrl(worktree), Is.EqualTo(Remote));
    }

    [Test]
    public void ReadOriginUrl_GivesNothingForADirectoryThatIsNotInARepository()
    {
        Assert.That(_reader.ReadOriginUrl(_root), Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    public void ReadOriginUrl_HandlesNoDirectory(string? directory)
    {
        Assert.That(_reader.ReadOriginUrl(directory), Is.Null);
    }

    [Test]
    public void ReadOriginUrl_GivesNothingWhenTheDirectoryDoesNotExist()
    {
        Assert.That(_reader.ReadOriginUrl(Path.Combine(_root, "no-such-place")), Is.Null);
    }

    [Test]
    public void ParseOriginUrl_IgnoresARemoteThatIsNotOrigin()
    {
        string[] lines =
        [
            "[remote \"upstream\"]",
            "\turl = https://github.com/someone/else.git",
            "[remote \"origin\"]",
            $"\turl = {Remote}",
        ];

        Assert.That(GitRemoteReader.ParseOriginUrl(lines), Is.EqualTo(Remote));
    }

    [Test]
    public void ParseOriginUrl_GivesNothingWhenThereIsNoOrigin()
    {
        Assert.That(GitRemoteReader.ParseOriginUrl(["[core]", "\tbare = false"]), Is.Null);
    }

    private string Repository(string name)
    {
        var repository = Path.Combine(_root, name);
        var git = Path.Combine(repository, ".git");
        Directory.CreateDirectory(git);
        File.WriteAllLines(Path.Combine(git, "config"), Config);

        return repository;
    }

    private string Worktree(string repository, string name)
    {
        var worktree = Path.Combine(_root, "features", name);
        Directory.CreateDirectory(worktree);

        var gitDirectory = Path.Combine(repository, ".git", "worktrees", name);
        Directory.CreateDirectory(gitDirectory);
        File.WriteAllText(Path.Combine(gitDirectory, "commondir"), "../..");

        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {gitDirectory}");

        return worktree;
    }
}
