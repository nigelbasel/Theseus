using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectCatalogTests;

namespace Theseus.Tests.Projects;

/// <summary>
/// The catalogue after the review of PR 1: a folder named like a project file, a file no branch can give, the
/// remembered primary (items 1 and 4), and branches read as branches, never as tags or options (item 8).
/// </summary>
[TestFixture]
public class ProjectCatalogReviewFixTests
{
    private TestRepository _repository = null!;
    private string _data = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _repository = TestRepository.Create();
        _data = Path.Combine(Path.GetTempPath(), "theseus-primaries-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_data);
    }

    [TearDown]
    public void TearDown()
    {
        _repository.Dispose();
        Directory.Delete(_data, recursive: true);
    }

    private ProjectPrimaryStore Primaries() => new(Path.Combine(_data, "project-primaries.json"));

    private IReadOnlyList<ProjectFileReading> Read(ProjectPrimaryStore? primaries = null) =>
        ProjectCatalog.Read([_repository.Folder], new GitCli(), primaries);

    [Test]
    public void AFolderNamedLikeAProjectFile_IsNotTakenForOne()
    {
        _repository.Commit("projects/notes.md/readme.txt", "A folder, not a project file.");
        _repository.Commit("projects/a-project.md", ProjectText("As on main"));
        _repository.Branch("owner/project");

        var readings = Read();

        Assert.That(readings.Select(reading => reading.Name), Is.EqualTo(new[] { "a-project" }));
        Assert.That(readings.Single().IsReadable, Is.True);
    }

    [Test]
    public void AFileOnlyARememberedPrimaryHas_AndNoRememberedEntry_IsUnreadable_NotACrash()
    {
        _repository.Commit("projects/a-project.md", ProjectText("A project"));
        _repository.Branch("owner/project");
        _repository.Commit("projects/b-project.md", ProjectText("Only on the primary", primary: "owner/other"));
        _repository.Checkout("main");
        var primaries = Primaries();
        Read(primaries);

        var readings = Read(primaries);

        Assert.That(readings.Single(reading => reading.Name == "b-project").Describe,
            Is.EqualTo("b-project: unreadable: projects/b-project.md could not be read on any branch"));
        Assert.That(readings.Single(reading => reading.Name == "a-project").IsReadable, Is.True);
    }

    [Test]
    public void OnceFound_ThePrimaryIsRemembered_AndReadAfterTheFolderSwitchesBranch()
    {
        _repository.Commit("projects/a-project.md", "# A project\n\n**Kit:** method-v0.1\n");
        _repository.Branch("owner/project");
        _repository.Commit("projects/a-project.md", ProjectText("As on the primary"));
        var primaries = Primaries();

        Assert.That(Read(primaries).Single().File!.Title, Is.EqualTo("As on the primary"), "found through the checked-out branch");

        _repository.Checkout("main");

        Assert.That(Read(primaries).Single().File!.Title, Is.EqualTo("As on the primary"), "read from the remembered primary");
        Assert.That(Read().Single().Status, Is.EqualTo(ProjectReadingStatus.Pinned), "without the memory, main's copy decides");
    }

    [Test]
    public void ARememberedPrimaryThatIsGone_IsFoundAgain()
    {
        _repository.Commit("projects/a-project.md", ProjectText("As on main"));
        _repository.Branch("owner/project");
        _repository.Commit("projects/a-project.md", ProjectText("As on the primary"));
        _repository.Checkout("main");
        var primaries = Primaries();
        primaries.Remember(_repository.Folder, "a-project", "owner/deleted");

        Assert.That(Read(primaries).Single().File!.Title, Is.EqualTo("As on the primary"));
        Assert.That(primaries.For(_repository.Folder).Single().Branch, Is.EqualTo("owner/project"));
    }

    [Test]
    public void ATagNamedLikeThePrimary_DoesNotWin()
    {
        _repository.Commit("projects/a-project.md", ProjectText("As on main"));
        _repository.Git("tag", "owner/project");
        _repository.Branch("owner/project");
        _repository.Commit("projects/a-project.md", ProjectText("As on the primary"));
        _repository.Checkout("main");

        Assert.That(Read().Single().File!.Title, Is.EqualTo("As on the primary"));
    }
}
