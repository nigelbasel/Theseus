using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// A projects folder pasted with the full stop of the sentence it came from (the owner, 2026-11-20: "E:\...\Proposals.")
/// is the folder Windows means, so the watcher, which fails on the typed form, watches it.
/// </summary>
[TestFixture]
public class ProjectsFolderPathTests
{
    [TestCase(@"C:\projects", @"C:\projects")]
    [TestCase(@"C:\projects.", @"C:\projects")]
    [TestCase(@"  C:\projects .  ", @"C:\projects")]
    [TestCase("", "")]
    [TestCase("   ", "")]
    [TestCase(null, "")]
    public void ATypedFolder_IsTheFolderWindowsMeans(string? typed, string meant) =>
        Assert.That(ProjectsFolderPath.Normalize(typed), Is.EqualTo(meant));

    [Test]
    public void AFolderTypedWithAFullStop_IsWatched_AndReadAsTheFolder()
    {
        using var projects = TestProjectsFolder.Create();
        projects.Write("a-project/project.md", TestProjectsFolder.ProjectText("A project"));
        using var watcher = new ProjectFolderWatcher(() => { }, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1));

        Assert.That(watcher.Watch(projects.Folder + "."), Is.EqualTo(ProjectFolderWatch.Started));
        Assert.That(ProjectCatalog.Read(projects.Folder + ".").Single().Folder, Is.EqualTo(projects.Home("a-project")));
    }
}
