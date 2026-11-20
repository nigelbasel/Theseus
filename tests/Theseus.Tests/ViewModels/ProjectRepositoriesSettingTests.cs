using Theseus.App.ViewModels;
using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

/// <summary>Settings' Project repositories: folders, one per line, empty by default (project-view AC 1).</summary>
[TestFixture]
public class ProjectRepositoriesSettingTests
{
    private string _folder = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "theseus-project-repositories-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public void IsEmpty_ForASettingsFileFromBeforeIt()
    {
        File.WriteAllText(Path.Combine(_folder, "settings.json"), "{}");

        Assert.That(new SettingsStore(Path.Combine(_folder, "settings.json")).Read().ProjectRepositories, Is.Empty);
        Assert.That(TheseusSettings.Default.ProjectRepositories, Is.Empty);
    }

    [Test]
    public void SavingThePanel_KeepsOneFolderPerLine_DroppingBlanksAndRepeats()
    {
        var store = new SettingsStore(Path.Combine(_folder, "settings.json"));
        var panel = new SettingsViewModel(store, () => { });

        panel.OpenCommand.Execute(null);
        panel.ProjectRepositories = "C:\\repos\\one\r\n\r\n  C:\\repos\\two  \r\nC:\\repos\\ONE\r\n";
        panel.SaveCommand.Execute(null);

        Assert.That(store.Read().ProjectRepositories, Is.EqualTo(new[] { "C:\\repos\\one", "C:\\repos\\two" }));

        var reopened = new SettingsViewModel(store, () => { });
        reopened.OpenCommand.Execute(null);

        Assert.That(reopened.ProjectRepositories, Is.EqualTo("C:\\repos\\one" + Environment.NewLine + "C:\\repos\\two"));
    }
}
