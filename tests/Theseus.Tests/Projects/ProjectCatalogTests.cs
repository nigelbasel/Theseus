using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// Which projects are read, and from where (1.1b AC 1 and 2): every subfolder of the projects folder holding a
/// project.md is a project, named by the folder and read as plain files; a folder that is not there, or a file that does
/// not parse, is unreadable with its reason, never left out.
/// </summary>
[TestFixture]
public class ProjectCatalogTests
{
    private TestProjectsFolder _projects = null!;

    [SetUp]
    public void SetUp() => _projects = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _projects.Dispose();

    private IReadOnlyList<ProjectFileReading> Read() => ProjectCatalog.Read(_projects.Folder);

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void NoFolderSet_ReadsNothing(string? folder) =>
        Assert.That(ProjectCatalog.Read(folder), Is.Empty, "a board with no projects folder behaves exactly as today (AC 13)");

    [Test]
    public void ASubfolderWithAProjectFile_IsAProject_NamedByTheFolder()
    {
        _projects.Write("a-project/project.md", TestProjectsFolder.ProjectText("A project"));

        var reading = Read().Single();

        Assert.That((reading.Name, reading.File!.Name, reading.File.Title), Is.EqualTo(("a-project", "a-project", "A project")));
        Assert.That(reading.Folder, Is.EqualTo(_projects.Home("a-project")));
    }

    [Test]
    public void SubfoldersWithoutAProjectFile_AndLooseFiles_AreLeftOut()
    {
        _projects.Write("a-project/project.md", TestProjectsFolder.ProjectText("A project"));
        _projects.Write("a-proposal/proposal.md", "# Not a project\n");
        _projects.Write("notes.md", "# Loose\n");

        Assert.That(Read().Select(reading => reading.Name), Is.EqualTo(new[] { "a-project" }));
    }

    [Test]
    public void Projects_AreListedInNameOrder()
    {
        _projects.Write("b-project/project.md", TestProjectsFolder.ProjectText("B"));
        _projects.Write("a-project/project.md", TestProjectsFolder.ProjectText("A"));

        Assert.That(Read().Select(reading => reading.Name), Is.EqualTo(new[] { "a-project", "b-project" }));
    }

    [Test]
    public void AFileAtV01_IsPinned()
    {
        _projects.Write("old/project.md", TestProjectsFolder.ProjectText("Old", kit: "method-v0.1"));

        Assert.That(Read().Single().Status, Is.EqualTo(ProjectReadingStatus.Pinned));
    }

    [Test]
    public void AFileOutOfShape_IsUnreadable_WithItsLine()
    {
        _projects.Write("broken/project.md", "no title here\n");

        var reading = Read().Single();

        Assert.That((reading.Status, reading.Folder), Is.EqualTo((ProjectReadingStatus.Unreadable, _projects.Home("broken"))));
        Assert.That(reading.Reason, Does.StartWith("line 1:"));
    }

    [Test]
    public void AProjectsFolderThatDoesNotExist_IsUnreadable()
    {
        var missing = Path.Combine(_projects.Folder, "not-here");

        var reading = ProjectCatalog.Read(missing).Single();

        Assert.That((reading.Name, reading.Status, reading.Reason),
            Is.EqualTo(("not-here", ProjectReadingStatus.Unreadable, "the projects folder does not exist")));
    }

    [Test]
    public void EachListedSlug_HasItsRecordsAttached()
    {
        _projects.Write("a-project/project.md",
            TestProjectsFolder.ProjectText("A project", body: TestProjectsFolder.OneObjective("started", "not-started")));
        _projects.Write("a-project/work/started/research.md", "# Research\n");

        var artefacts = Read().Single().Artefacts;

        Assert.That(artefacts["started"].Files, Is.EquivalentTo(new[] { "research.md" }));
        Assert.That(artefacts["not-started"].Source.Exists, Is.False);
    }

    [Test]
    public void Reading_StartsNoProcess()
    {
        var output = Path.GetDirectoryName(typeof(ProjectCatalog).Assembly.Location)!;
        var source = Path.GetFullPath(Path.Combine(output, "..", "..", "..", "..", "..", "src", "Theseus.Core", "Projects"));

        Assert.That(Directory.Exists(source), Is.True, $"the source folder is not at {source}: this test would guard nothing");

        var starting = Directory.EnumerateFiles(source, "*.cs")
            .Where(file => File.ReadAllText(file).Contains("Process", StringComparison.Ordinal))
            .Select(Path.GetFileName);

        Assert.That(starting, Is.Empty, "records are read as plain files, never with git or another process (1.1b AC 2)");
    }
}
