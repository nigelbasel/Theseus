using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

/// <summary>
/// What a card's branch line says (card-branches spec AC 3 to 9). A card that declares no repos says exactly what it
/// said before, word for word.
/// </summary>
[TestFixture]
public class BranchLinesTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-branch-lines-{Guid.NewGuid():N}");
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
    public void NoReposDeclared_ReadsTheWorkingDirectoryAsBefore()
    {
        var working = Directory.CreateDirectory(Path.Combine(_root, "notes")).FullName;
        var git = Directory.CreateDirectory(Path.Combine(working, ".git")).FullName;
        File.WriteAllText(Path.Combine(git, "HEAD"), "ref: refs/heads/main\n");

        var line = BranchLines.For(working, []);

        Assert.That(line.Text, Is.EqualTo("on main"));
        Assert.That(line.Tooltip, Is.EqualTo("The branch checked out in this working directory: main"));
        Assert.That(line.Differs, Is.False);
    }

    [Test]
    public void NoReposDeclared_AndNoRepoAtTheWorkingDirectory_ShowsNothing()
    {
        var line = BranchLines.For(_root, []);

        Assert.That(line.Text, Is.Empty);
        Assert.That(line.Tooltip, Is.Empty);
    }

    [Test]
    public void OneRepo_SaysItsBranch_AndNamesItInTheTooltip()
    {
        var line = BranchLines.For(_root, [Repo("api", "user/feature")]);

        Assert.That(line.Text, Is.EqualTo("on user/feature"));
        Assert.That(line.Tooltip, Is.EqualTo("The branch checked out in api: user/feature"));
        Assert.That(line.Differs, Is.False);
    }

    [Test]
    public void SeveralReposOnOneBranch_SayTheBranchAndHowMany()
    {
        var line = BranchLines.For(_root, [Repo("api", "user/feature"), Repo("ui", "user/feature"), Repo("db", "user/feature")]);

        Assert.That(line.Text, Is.EqualTo("on user/feature · 3 repos"));
        Assert.That(line.Tooltip, Is.EqualTo("Branches of this card's repos:\napi: user/feature\nui: user/feature\ndb: user/feature"));
        Assert.That(line.Differs, Is.False);
    }

    [Test]
    public void ReposOnDifferentBranches_AreMarked_AndListedSoTheOddOneOutShows()
    {
        var line = BranchLines.For(_root, [Repo("api", "user/feature"), Repo("ui", "main")]);

        Assert.That(line.Text, Is.EqualTo("branches differ"));
        Assert.That(line.Tooltip, Is.EqualTo("Branches of this card's repos:\napi: user/feature\nui: main"));
        Assert.That(line.Differs, Is.True);
    }

    [Test]
    public void ADetachedRepoAmongOthers_CountsAsDiffering()
    {
        var line = BranchLines.For(_root, [Repo("api", "user/feature"), Repo("ui", "detached at 1a2b3c4")]);

        Assert.That(line.Differs, Is.True);
        Assert.That(line.Tooltip, Does.Contain("ui: detached at 1a2b3c4"));
    }

    [Test]
    public void ARepoWhoseBranchCannotBeRead_CountsAsDiffering_AndSaysSo()
    {
        var line = BranchLines.For(_root, [Repo("api", "user/feature"), Repo("ui", null)]);

        Assert.That(line.Differs, Is.True);
        Assert.That(line.Tooltip, Does.Contain("ui: branch not readable"));
    }

    private static CardRepo Repo(string name, string? branch) => new(Path.Combine("x", name), name, branch);
}
