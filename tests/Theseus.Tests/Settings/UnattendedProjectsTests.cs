using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Core.Settings;

namespace Theseus.Tests.Settings;

/// <summary>
/// The owner's per-project unattended setting (2.4 W2): kept in Theseus's settings, off by default, set in Project
/// properties; nothing a project's file says makes a project unattended.
/// </summary>
[TestFixture]
public class UnattendedProjectsTests
{
    private string _directory = string.Empty;

    [SetUp]
    public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), $"theseus-unattended-{Guid.NewGuid():N}");

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void NoProject_IsUnattended_ByDefault()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TheseusSettings.Default.UnattendedProjects, Is.Empty);
            Assert.That(UnattendedProjects.IsOn("bug-fixing", TheseusSettings.Default.UnattendedProjects), Is.False);
            Assert.That(new ProjectPropertiesViewModel("bug-fixing", "Bug Fixing", "#2F7D32", @"<root>\bug-fixing\project.md").IsUnattended,
                Is.False);
        });
    }

    [Test]
    public void AProjectIsUnattended_OnlyWhileTheSettingsNameIt_AndACardInNoProjectNever()
    {
        IReadOnlyList<string> named = ["Bug-Fixing"];

        Assert.Multiple(() =>
        {
            Assert.That(UnattendedProjects.IsOn("bug-fixing", named), Is.True, "folder names compare as Windows does");
            Assert.That(UnattendedProjects.IsOn("theseus", named), Is.False);
            Assert.That(UnattendedProjects.IsOn(null, named), Is.False);
        });
    }

    [Test]
    public void TheSetting_SurvivesARestart()
    {
        var store = new SettingsStore(Path.Combine(_directory, "settings.json"));

        store.Write(store.Read() with { UnattendedProjects = ["bug-fixing"] });

        Assert.That(new SettingsStore(Path.Combine(_directory, "settings.json")).Read().UnattendedProjects, Is.EqualTo(new[] { "bug-fixing" }));
    }

    [Test]
    public void SwitchingItOnOrOff_KeepsTheOtherProjects()
    {
        IReadOnlyList<string> named = ["theseus"];

        Assert.Multiple(() =>
        {
            Assert.That(UnattendedProjects.Set(named, "bug-fixing", on: true), Is.EquivalentTo(new[] { "theseus", "bug-fixing" }));
            Assert.That(UnattendedProjects.Set(["theseus", "Bug-Fixing"], "bug-fixing", on: false), Is.EqualTo(new[] { "theseus" }));
            Assert.That(UnattendedProjects.Set(["bug-fixing"], "BUG-FIXING", on: true), Has.Count.EqualTo(1));
        });
    }
}
