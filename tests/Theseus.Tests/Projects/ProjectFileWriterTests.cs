using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// An owner's edit is written only when the file still holds the text Theseus read, so it never overwrites a session's
/// change made meanwhile (1.1c AC 9).
/// </summary>
[TestFixture]
public class ProjectFileWriterTests
{
    private TestProjectsFolder _projects = null!;

    [SetUp]
    public void SetUp() => _projects = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _projects.Dispose();

    private ProjectFileReading ReadOne() => ProjectCatalog.Read(_projects.Folder).Single();

    [Test]
    public void AnEdit_IsWritten_WhenTheFileIsAsRead()
    {
        var path = _projects.Write("a-project/project.md", TestProjectsFolder.ProjectText("A project", body: TestProjectsFolder.OneObjective()));

        ProjectFileWriter.Apply(ReadOne(), text => ProjectFileEditor.RenameObjective(text, "0.1", "Renamed"));

        Assert.That(File.ReadAllText(path), Does.Contain("### Objective 0.1: Renamed"));
    }

    [Test]
    public void AnEdit_IsRefused_WhenASessionChangedTheFileSinceItWasRead()
    {
        var path = _projects.Write("a-project/project.md", TestProjectsFolder.ProjectText("A project", body: TestProjectsFolder.OneObjective()));
        var read = ReadOne();
        File.AppendAllText(path, "A session's line.\n");

        Assert.Throws<ProjectEditRefusedException>(() => ProjectFileWriter.Apply(read, text => ProjectFileEditor.RenameObjective(text, "0.1", "Renamed")));
        Assert.That(File.ReadAllText(path), Does.EndWith("A session's line.\n"), "the session's change is kept");
    }

    [Test]
    public void AFileWithAByteOrderMark_KeepsIt()
    {
        var path = _projects.Write("a-project/project.md", string.Empty);
        File.WriteAllText(path, TestProjectsFolder.ProjectText("A project", body: TestProjectsFolder.OneObjective()), new System.Text.UTF8Encoding(true));

        ProjectFileWriter.Apply(ReadOne(), text => ProjectFileEditor.RenameObjective(text, "0.1", "Renamed"));

        Assert.That(File.ReadAllBytes(path).Take(3), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.That(File.ReadAllText(path), Does.Contain("### Objective 0.1: Renamed"));
    }
}
