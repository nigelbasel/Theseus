using Theseus.App.ViewModels;
using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

/// <summary>Settings' Projects folder and Worktrees folder: empty by default, kept as typed less spaces (1.1b AC 1, 11).</summary>
[TestFixture]
public class ProjectFoldersSettingTests
{
    private string _folder = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "theseus-project-folders-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public void AreEmpty_ForASettingsFileFromBeforeThem_EvenOneListingProjectRepositories()
    {
        File.WriteAllText(Path.Combine(_folder, "settings.json"), """{ "ProjectRepositories": [ "C:\\repos\\home" ] }""");

        var read = new SettingsStore(Path.Combine(_folder, "settings.json")).Read();

        Assert.That((read.ProjectsFolder, read.WorktreesFolder), Is.EqualTo((string.Empty, string.Empty)));
        Assert.That((TheseusSettings.Default.ProjectsFolder, TheseusSettings.Default.WorktreesFolder), Is.EqualTo((string.Empty, string.Empty)));
    }

    [Test]
    public void ChangingThePanel_KeepsEachFolder_Trimmed()
    {
        var store = new SettingsStore(Path.Combine(_folder, "settings.json"));
        var panel = new SettingsViewModel(store, () => { });

        panel.OpenCommand.Execute(null);
        panel.ProjectsFolder = "  C:\\projects  ";
        panel.WorktreesFolder = "C:\\work ";

        Assert.That((store.Read().ProjectsFolder, store.Read().WorktreesFolder), Is.EqualTo(("C:\\projects", "C:\\work")));

        var reopened = new SettingsViewModel(store, () => { });
        reopened.OpenCommand.Execute(null);

        Assert.That((reopened.ProjectsFolder, reopened.WorktreesFolder), Is.EqualTo(("C:\\projects", "C:\\work")));
    }

    [Test]
    public void TheIntro_ShowsUntilAProjectsFolderIsSet_AndStaysWhileOneIsTyped()
    {
        var store = new SettingsStore(Path.Combine(_folder, "settings.json"));
        var panel = new SettingsViewModel(store, () => { });

        panel.OpenCommand.Execute(null);
        Assert.That(panel.ShowsProjectsIntro, Is.True, "no projects folder saved (1.1b AC 11)");

        panel.ProjectsFolder = "C:\\pro";
        Assert.That(panel.ShowsProjectsIntro, Is.True, "decided when Settings opens, so it does not vanish mid-typing");

        panel.ProjectsFolder = "C:\\projects";
        panel.OpenCommand.Execute(null);

        Assert.That(panel.ShowsProjectsIntro, Is.False);
    }
}
