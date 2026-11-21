using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Links;

namespace Theseus.Tests.Links;

/// <summary>
/// The holdout folder by convention (1.1e AC 6c): a folder per project under Theseus's data folder unless the owner chose
/// one, created on first use; whether it has scenarios; the scenario writer's role and card; Options showing the folder in
/// use.
/// </summary>
[TestFixture]
public class HoldoutLocationTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "theseus-holdout-location-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void WithNoFolderChosen_EachProjectHasItsOwnDefault_AndAdHocCardsOneTogether()
    {
        var theseus = HoldoutLocation.For("", "theseus", _root);

        Assert.That(theseus, Is.EqualTo(new HoldoutLocation(Path.Combine(_root, "Theseus", "holdouts", "theseus"), true)));
        Assert.That(HoldoutLocation.For(null, null, _root).Folder, Is.EqualTo(Path.Combine(_root, "Theseus", "holdouts", "ad-hoc")));
        Assert.That(theseus.IsMissing, Is.False, "a default is made on first use, never missing");
    }

    [Test]
    public void AFolderTheOwnerChose_IsKept_AndIsMissingWhenItIsNotThere()
    {
        var chosen = HoldoutLocation.For(@"  C:\no\such\holdout ", "theseus", _root);

        Assert.That((chosen.Folder, chosen.IsDefault, chosen.IsMissing), Is.EqualTo((@"C:\no\such\holdout", false, true)));
    }

    [Test]
    public void Ensure_MakesTheDefault_ButNeverAChosenFolder()
    {
        var theseus = HoldoutLocation.For("", "theseus", _root);
        var chosen = new HoldoutLocation(Path.Combine(_root, "chosen"), false);

        theseus.Ensure();
        chosen.Ensure();

        Assert.That((Directory.Exists(theseus.Folder), Directory.Exists(chosen.Folder)), Is.EqualTo((true, false)));
    }

    [Test]
    public void Scenarios_AreFilesOutsideTheResultsFolderAndGit()
    {
        var holdout = new HoldoutLocation(Path.Combine(_root, "h"), true);

        Assert.That(holdout.HasScenarios, Is.False, "no folder yet");

        Directory.CreateDirectory(Path.Combine(holdout.Folder, "results"));
        Directory.CreateDirectory(Path.Combine(holdout.Folder, ".git"));
        File.WriteAllText(Path.Combine(holdout.Folder, "results", "report.md"), "x");
        File.WriteAllText(Path.Combine(holdout.Folder, ".git", "HEAD"), "x");
        File.WriteAllText(Path.Combine(holdout.Folder, ".gitignore"), "x");

        Assert.That(holdout.HasScenarios, Is.False, "reports and the repository's own files are not scenarios");

        Directory.CreateDirectory(Path.Combine(holdout.Folder, "board"));
        File.WriteAllText(Path.Combine(holdout.Folder, "board", "stop.md"), "x");

        Assert.That(holdout.HasScenarios, Is.True);
    }

    [Test]
    public void AScenarioWriter_RunsAsTheHoldoutsAuthor_AndWritesScenariosOnly()
    {
        var writer = ScenarioWriterCard.For("Theseus", @"<root>\holdouts\theseus", @"<projects>\theseus");

        Assert.That(RoleEnvironment.For(WorkstreamRole.ScenarioWriter)[RoleEnvironment.HoldoutAuthorVariable], Is.EqualTo("1"));
        Assert.That(RoleEnvironment.For(WorkstreamRole.None), Is.Empty, "a building session never has it");
        Assert.That((writer.Title, writer.WorkingDirectory, writer.AddDirectories),
            Is.EqualTo(("Write holdout scenarios for Theseus", @"<root>\holdouts\theseus", new[] { @"<projects>\theseus" })));
        Assert.That(writer.Prompt, Does.Contain("you write scenarios, never code"));
    }

    [Test]
    public void Options_ShowsTheFolderInUse_MarkedDefault_UntilOneIsChosen()
    {
        var store = new Theseus.Core.Settings.SettingsStore(Path.Combine(_root, "settings.json"));
        var settings = new SettingsViewModel(store, () => { });

        Assert.That(settings.HoldoutFolderInUse, Does.EndWith(Path.Combine("holdouts", "<project folder name>") + " (default)"));

        settings.HoldoutFolder = @"C:\clones\holdout";

        Assert.That(settings.HoldoutFolderInUse, Is.EqualTo(@"C:\clones\holdout"));
    }
}
