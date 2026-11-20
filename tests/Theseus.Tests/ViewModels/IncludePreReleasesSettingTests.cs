using Theseus.App.ViewModels;
using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

[TestFixture]
public class IncludePreReleasesSettingTests
{
    private string _folder = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "theseus-prerelease-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    [Test]
    public void IsOffForASettingsFileFromBeforeIt()
    {
        File.WriteAllText(Path.Combine(_folder, "settings.json"), "{}");

        Assert.That(new SettingsStore(Path.Combine(_folder, "settings.json")).Read().IncludePreReleases, Is.False);
    }

    [Test]
    public void SavingThePanelKeepsTheChoice()
    {
        var store = new SettingsStore(Path.Combine(_folder, "settings.json"));
        var panel = new SettingsViewModel(store, () => { });

        panel.OpenCommand.Execute(null);
        panel.IncludePreReleases = true;
        panel.SaveCommand.Execute(null);

        Assert.That(store.Read().IncludePreReleases, Is.True);

        var reopened = new SettingsViewModel(store, () => { });
        reopened.OpenCommand.Execute(null);

        Assert.That(reopened.IncludePreReleases, Is.True);
    }
}
