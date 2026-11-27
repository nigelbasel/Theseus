using System.Text.Json.Nodes;
using Theseus.App.ViewModels;
using Theseus.Core.Settings;
using Theseus.Core.Trust;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Options › Folder trust says what it did where it can be seen: Nigel pressed Trust these folders… on 2026-11-23 and had
/// "no idea if it adjusted the claude trust".
/// </summary>
[TestFixture]
public class FolderTrustViewModelTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "theseus-trust-vm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "repo", ".git"));
        File.WriteAllText(Path.Combine(_root, "config.json"), new JsonObject().ToJsonString());
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void TrustingFolders_SaysWhatItDid_InTheStatusBarToo()
    {
        var said = new List<string>();
        var section = new FolderTrustViewModel(
            new ClaudeFolderTrust(Path.Combine(_root, "config.json"), Path.Combine(_root, "undo.json")),
            () => [Path.Combine(_root, "repo")])
        {
            ConfirmChange = _ => true,
            Said = said.Add,
        };
        section.Load();

        section.TrustCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(said, Is.EqualTo(new[] { "Trusted 1 folder in Claude Code." }));
            Assert.That(section.Line, Does.StartWith("Claude Code will not ask"), "the section's own line changes too");
        });
    }

    [Test]
    public void SwitchingOn_ShowsTheConsentFirst_AndDeclinedChangesNothing()
    {
        var (section, settings) = Standing();
        string? shown = null;
        section.ConfirmConsent = consent =>
        {
            shown = consent;

            return false;
        };
        section.Load();

        section.SwitchOnCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(shown, Does.Contain("Trust the folders my cards use"));
            Assert.That(settings.Settings.TrustCardFolders, Is.False);
            Assert.That(section.Result, Is.EqualTo("Nothing was changed."));
            Assert.That(File.Exists(Path.Combine(_root, "undo.json")), Is.False, "nothing written to Claude Code's file");
        });
    }

    [Test]
    public void SwitchingOn_TrustsWhatTheCardsNeedNow_AndUndoSwitchesItOff()
    {
        var (section, settings) = Standing();
        section.ConfirmConsent = _ => true;
        section.Load();

        section.SwitchOnCommand.Execute(null);
        var afterOn = (settings.Settings.TrustCardFolders, section.Line, section.Result);
        section.UndoCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(afterOn.TrustCardFolders, Is.True);
            Assert.That(afterOn.Result, Is.EqualTo("Switched on, and trusted 1 folder in Claude Code."));
            Assert.That(afterOn.Line, Does.StartWith("Claude Code will not ask"));
            Assert.That(settings.Settings.TrustCardFolders, Is.False);
            Assert.That(section.Line, Does.StartWith("Claude Code will ask you to trust 1 folder"));
        });
    }

    private (FolderTrustViewModel Section, MemorySettingsStore Settings) Standing()
    {
        var settings = new MemorySettingsStore { Settings = TheseusSettings.Default with { ClonesFolder = _root } };
        var standing = new StandingFolderTrust(
            new ClaudeFolderTrust(Path.Combine(_root, "config.json"), Path.Combine(_root, "undo.json")), settings, () => [],
            Path.Combine(_root, "user"));

        return (new FolderTrustViewModel(standing, () => [Path.Combine(_root, "repo")]), settings);
    }
}
