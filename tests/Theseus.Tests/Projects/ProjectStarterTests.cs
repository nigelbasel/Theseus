using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// New project… (1.1b AC 7 and 8; 1.1c AC 6): a new home folder with the shipped starter, its title filled in, read as a
/// project at once; a name that exists, or is not a folder-and-identity shape, is refused.
/// </summary>
[TestFixture]
public class ProjectStarterTests
{
    private TestProjectsFolder _projects = null!;

    [SetUp]
    public void SetUp() => _projects = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _projects.Dispose();

    [Test]
    public void TheShippedStarter_IsTheRepositorysStarter()
    {
        var repository = File.ReadAllText(Path.Combine(RepositoryRoot(), "method", "starter", "project.md"));

        Assert.That(ProjectStarter.StarterText(), Is.EqualTo(repository));
    }

    [Test]
    public void ANewProjectsFirstPhase_HasAName()
    {
        ProjectStarter.Start(_projects.Folder, "my-project");

        Assert.That(ProjectCatalog.Read(_projects.Folder).Single().File!.Phases[0].Name, Is.EqualTo("Specify"));
    }

    [Test]
    public void ANewProject_IsReadable_WithItsNameAsTitle_AndAnEmptyWorkFolder()
    {
        var home = ProjectStarter.Start(_projects.Folder, "my-project");

        Assert.That(home, Is.EqualTo(_projects.Home("my-project")));
        Assert.That(Directory.EnumerateFileSystemEntries(Path.Combine(home, "work")), Is.Empty);

        var reading = ProjectCatalog.Read(_projects.Folder).Single();

        Assert.That((reading.Name, reading.Reason, reading.File!.Title, reading.File.Kit),
            Is.EqualTo(("my-project", (string?)null, "my-project", MethodKits.Current)));
    }

    [Test]
    public void ANewProjectsDetails_FillTheStarter_OnOneLineEach_AndWhatIsLeftEmptyKeepsItsPrompt()
    {
        var home = ProjectStarter.Start(_projects.Folder,
            new NewProjectDetails("shop", "The shop", "Customers pay\r\nwithout calling us.", "dana/shop"));
        var file = ProjectCatalog.Read(_projects.Folder).Single().File!;
        var text = File.ReadAllText(Path.Combine(home, "project.md"));

        Assert.That((file.Title, file.PrimaryBranch), Is.EqualTo(("The shop", "dana/shop")));
        Assert.That(text, Does.Contain("Customers pay without calling us."));

        var bare = ProjectStarter.Filled(new NewProjectDetails("bare", "", "", ""));

        Assert.That(bare, Does.StartWith("# bare"));
        Assert.That(bare, Does.Contain("One paragraph: the outcome the whole project exists for."));
        Assert.That(bare, Does.Contain("**Primary branch:** <user>/<project>"));
    }

    [Test]
    public void ANameThatExists_IsRefused()
    {
        ProjectStarter.Start(_projects.Folder, "my-project");

        Assert.That(ProjectStarter.Refusal(_projects.Folder, "my-project"), Is.EqualTo("my-project already exists in the projects folder."));
        Assert.Throws<InvalidOperationException>(() => ProjectStarter.Start(_projects.Folder, "my-project"));
    }

    [TestCase("")]
    [TestCase("My Project")]
    [TestCase("my_project")]
    [TestCase("../elsewhere")]
    [TestCase("con\\nul")]
    public void ANameNotOfTheShape_IsRefused(string name) =>
        Assert.That(ProjectStarter.Refusal(_projects.Folder, name), Does.StartWith("A project's name is its folder's name"));

    [TestCase("con")]
    [TestCase("nul")]
    [TestCase("com1")]
    [TestCase("lpt9")]
    public void ANameWindowsKeepsForADevice_IsRefused(string name) =>
        Assert.That(ProjectStarter.Refusal(_projects.Folder, name), Is.EqualTo($"{name} is a name Windows keeps for a device, so it cannot name a folder."));

    [Test]
    public void StartingOverAnExistingProject_NeverOverwritesItsFile()
    {
        var home = Directory.CreateDirectory(_projects.Home("raced")).FullName;
        File.WriteAllText(Path.Combine(home, "project.md"), "# Theirs\n");

        Assert.Throws<InvalidOperationException>(() => ProjectStarter.Start(_projects.Folder, "raced"));
        Assert.That(File.ReadAllText(Path.Combine(home, "project.md")), Is.EqualTo("# Theirs\n"));
    }

    [Test]
    public void NoProjectsFolder_OrOneNotThere_IsRefused()
    {
        Assert.That(ProjectStarter.Refusal("", "my-project"), Is.EqualTo("Set the projects folder in Tools > Options first."));
        Assert.That(ProjectStarter.Refusal(Path.Combine(_projects.Folder, "absent"), "my-project"), Does.EndWith("does not exist."));
    }

    private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
