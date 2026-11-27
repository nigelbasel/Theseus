using System.Text.Json.Nodes;
using Theseus.App.ViewModels;
using Theseus.Core.Settings;
using Theseus.Core.Trust;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Options › Folders, Base folders kept trusted (objective 1.3f): set once, with Add…, Reset and Undo, each saying what it
/// did where it can be seen. Nigel pressed a trust button on 2026-11-23 and had "no idea if it adjusted the claude trust".
/// </summary>
[TestFixture]
public class FolderTrustViewModelTests
{
    private string _root = null!;
    private string _config = null!;
    private string _work = null!;
    private MemorySettingsStore _settings = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "theseus-trust-vm-" + Guid.NewGuid().ToString("N"));
        _config = Path.Combine(Directory.CreateDirectory(_root).FullName, "config.json");
        _work = Directory.CreateDirectory(Path.Combine(_root, "work")).FullName;
        File.WriteAllText(_config, new JsonObject().ToJsonString());
        _settings = new MemorySettingsStore { Settings = TheseusSettings.Default with { WorktreesFolder = _work } };
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Reset_TrustsTheDefaults_AndSaysSoInTheStatusBarToo()
    {
        string? said = null;
        var section = Section();
        section.Said = text => said = text;

        section.ResetCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(section.Folders, Is.EqualTo(_work));
            Assert.That(said, Does.Contain("Trusted").And.Contain(Spelled(_work)));
            Assert.That(section.Result, Is.EqualTo(said));
        });
    }

    [Test]
    public void Add_RefusesAFolderInsideARepository_AndSaysWhy()
    {
        var repo = Directory.CreateDirectory(Path.Combine(_root, "repo")).FullName;
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        var inside = Directory.CreateDirectory(Path.Combine(repo, "docs")).FullName;
        var section = Section();
        section.ChooseFolder = () => inside;

        section.AddCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(section.Result, Does.StartWith("Not added").And.Contain("inside the repository"));
            Assert.That(_settings.Settings.BaseFolders, Is.Null);
        });
    }

    [Test]
    public void Add_WhenTheOwnerCancels_ChangesNothing()
    {
        var section = Section();
        section.ChooseFolder = () => null;

        section.AddCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(section.Result, Is.EqualTo("Nothing was changed."));
            Assert.That(_settings.Settings.BaseFolders, Is.Null);
        });
    }

    [Test]
    public void Undo_EmptiesTheBaseFolders_SoTheFirstRunStepAsksAgain()
    {
        var section = Section();
        section.ResetCommand.Execute(null);

        section.UndoCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(section.IsSet, Is.False);
            Assert.That(section.Folders, Does.StartWith("None"));
            Assert.That(JsonNode.Parse(File.ReadAllText(_config))?["projects"]?[Spelled(_work)], Is.Null, "the record Theseus made is gone");
        });
    }

    private FolderTrustViewModel Section() =>
        new(new BaseFolderTrust(new ClaudeFolderTrust(_config, Path.Combine(_root, "undo.json")), _settings, () => [], Path.Combine(_root, "user")));

    private static string Spelled(string path)
    {
        var forward = path.Replace('\\', '/').TrimEnd('/');

        return char.ToUpperInvariant(forward[0]) + forward[1..];
    }
}
