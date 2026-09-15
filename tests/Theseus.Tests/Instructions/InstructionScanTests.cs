using Theseus.Core.Instructions;

namespace Theseus.Tests.Instructions;

[TestFixture]
public class InstructionScanTests
{
    private string _root = string.Empty;
    private string _claude = string.Empty;
    private string _managed = string.Empty;
    private string _repo = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-instructions-{Guid.NewGuid():N}");
        _claude = Path.Combine(_root, "home", ".claude");
        _managed = Path.Combine(_root, "managed", "CLAUDE.md");
        _repo = Path.Combine(_root, "work", "repo");
        Directory.CreateDirectory(_claude);
        Directory.CreateDirectory(_repo);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void For_ReadsTheManagedFileFirstThenTheUsersOwn()
    {
        Write(_managed);
        Write(Path.Combine(_claude, "CLAUDE.md"));
        Write(Path.Combine(_repo, "CLAUDE.md"));

        var files = Scan();

        Assert.That(files[0].Scope, Is.EqualTo(InstructionScope.Managed));
        Assert.That(files[1].Scope, Is.EqualTo(InstructionScope.User));
        Assert.That(files[^1].Scope, Is.EqualTo(InstructionScope.Project));
    }

    [Test]
    public void For_ReadsFromTheRootDownToTheWorkingDirectory()
    {
        var above = Write(Path.Combine(_root, "work", "CLAUDE.md"));
        var here = Write(Path.Combine(_repo, "CLAUDE.md"));

        var paths = Paths(Scan());

        Assert.That(paths.IndexOf(above), Is.LessThan(paths.IndexOf(here)));
        Assert.That(Scan().Single(file => file.Path == above).Scope, Is.EqualTo(InstructionScope.Ancestor));
    }

    [Test]
    public void For_ReadsTheLocalFileAfterTheOneBesideIt()
    {
        var shared = Write(Path.Combine(_repo, "CLAUDE.md"));
        var local = Write(Path.Combine(_repo, "CLAUDE.local.md"));

        var files = Scan();

        Assert.That(Paths(files).IndexOf(local), Is.GreaterThan(Paths(files).IndexOf(shared)));
        Assert.That(files.Single(file => file.Path == local).Scope, Is.EqualTo(InstructionScope.Local));
    }

    [Test]
    public void For_FindsTheProjectFileKeptInsideDotClaude()
    {
        var inside = Write(Path.Combine(_repo, ".claude", "CLAUDE.md"));

        Assert.That(Scan().Single(file => file.Path == inside).Scope, Is.EqualTo(InstructionScope.Project));
    }

    [Test]
    public void For_PutsAnImportRightAfterTheFileThatImportsIt()
    {
        var importer = Write(Path.Combine(_repo, "CLAUDE.md"), "See @docs/guide.md for more.");
        var imported = Write(Path.Combine(_repo, "docs", "guide.md"));

        var files = Scan();
        var at = Paths(files).IndexOf(importer);

        Assert.That(files[at + 1].Path, Is.EqualTo(imported));
        Assert.That(files[at + 1].Scope, Is.EqualTo(InstructionScope.Import));
        Assert.That(files[at + 1].ImportDepth, Is.EqualTo(1));
    }

    [Test]
    public void For_ResolvesAnImportFromTheHomeDirectory()
    {
        Write(Path.Combine(_claude, "CLAUDE.md"), "@~/shared/standards.md");
        var imported = Write(Path.Combine(_root, "home", "shared", "standards.md"));

        Assert.That(Paths(Scan()), Does.Contain(imported));
    }

    [Test]
    public void For_FollowsImportsNoMoreThanFourHops()
    {
        Write(Path.Combine(_repo, "CLAUDE.md"), "@one.md");
        Write(Path.Combine(_repo, "one.md"), "@two.md");
        Write(Path.Combine(_repo, "two.md"), "@three.md");
        Write(Path.Combine(_repo, "three.md"), "@four.md");
        var fourth = Write(Path.Combine(_repo, "four.md"), "@five.md");
        var fifth = Write(Path.Combine(_repo, "five.md"));

        var files = Scan();

        Assert.That(files.Single(file => file.Path == fourth).ImportDepth, Is.EqualTo(4));
        Assert.That(Paths(files), Does.Not.Contain(fifth));
    }

    [Test]
    public void For_ListsFilesThatImportEachOtherOnce()
    {
        var first = Write(Path.Combine(_repo, "CLAUDE.md"), "@other.md");
        var second = Write(Path.Combine(_repo, "other.md"), "@CLAUDE.md");

        var mine = Paths(Scan()).Where(path => path.StartsWith(_root, StringComparison.OrdinalIgnoreCase)).ToList();

        Assert.That(mine, Is.EqualTo(new[] { first, second }));
    }

    [Test]
    public void For_IgnoresImportsWrittenAsCode()
    {
        Write(Path.Combine(_repo, "CLAUDE.md"), "Mention `@span.md` only.\n```\n@fenced.md\n```\n");
        var span = Write(Path.Combine(_repo, "span.md"));
        var fenced = Write(Path.Combine(_repo, "fenced.md"));

        Assert.That(Paths(Scan()), Does.Not.Contain(span).And.Not.Contain(fenced));
    }

    [Test]
    public void For_ReadsARuleAtStartUnlessItNamesThePathsItIsFor()
    {
        var always = Write(Path.Combine(_repo, ".claude", "rules", "style.md"), "Use tabs.");
        var scoped = Write(Path.Combine(_repo, ".claude", "rules", "api", "api.md"),
            "---\npaths:\n  - \"src/api/**/*.ts\"\n---\nValidate input.");

        var files = Scan();

        Assert.That(files.Single(file => file.Path == always).Load, Is.EqualTo(InstructionLoad.AtStart));
        Assert.That(files.Single(file => file.Path == scoped).Load, Is.EqualTo(InstructionLoad.OnDemand));
        Assert.That(files.Single(file => file.Path == scoped).When, Does.Contain("src/api/**/*.ts"));
    }

    [Test]
    public void For_ReadsTheUsersOwnRulesBeforeTheProjects()
    {
        var mine = Write(Path.Combine(_claude, "rules", "preferences.md"));
        var project = Write(Path.Combine(_repo, ".claude", "rules", "style.md"));

        var paths = Paths(Scan());

        Assert.That(paths.IndexOf(mine), Is.LessThan(paths.IndexOf(project)));
    }

    [Test]
    public void For_ListsInstructionsBelowTheWorkingDirectoryAsReadOnDemand()
    {
        var nested = Write(Path.Combine(_repo, "src", "api", "CLAUDE.md"));

        var file = Scan().Single(candidate => candidate.Path == nested);

        Assert.That(file.Load, Is.EqualTo(InstructionLoad.OnDemand));
        Assert.That(file.When, Does.Contain(Path.Combine("src", "api")));
    }

    [Test]
    public void For_DoesNotSearchBuildOutputOrLookTooDeep()
    {
        var packaged = Write(Path.Combine(_repo, "node_modules", "lib", "CLAUDE.md"));
        var deep = Write(Path.Combine(_repo, "a", "b", "c", "d", "CLAUDE.md"));

        Assert.That(Paths(Scan()), Does.Not.Contain(packaged).And.Not.Contain(deep));
    }

    [Test]
    public void For_ShowsAnAddedDirectorysInstructionsAsNotRead()
    {
        var added = Path.Combine(_root, "shared");
        var file = Write(Path.Combine(added, "CLAUDE.md"));

        Assert.That(Scan([added]).Single(candidate => candidate.Path == file).Load, Is.EqualTo(InstructionLoad.NotLoaded));
        Assert.That(Scan([added], additionalLoad: true).Single(candidate => candidate.Path == file).Load,
            Is.EqualTo(InstructionLoad.AtStart));
    }

    [Test]
    public void For_StillListsTheUsersFilesWhenTheWorkingDirectoryIsGone()
    {
        var mine = Write(Path.Combine(_claude, "CLAUDE.md"));

        var files = InstructionScan.For(Path.Combine(_root, "never-existed"), [], _claude, _managed, false);

        Assert.That(Paths(files), Is.EqualTo(new[] { mine }));
    }

    private IReadOnlyList<InstructionFile> Scan(IReadOnlyList<string>? added = null, bool additionalLoad = false) =>
        InstructionScan.For(_repo, added ?? [], _claude, _managed, additionalLoad);

    private static List<string> Paths(IReadOnlyList<InstructionFile> files) => [.. files.Select(file => file.Path)];

    private static string Write(string path, string text = "Instructions.")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);

        return Path.GetFullPath(path);
    }
}
