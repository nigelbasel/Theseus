using System.Text.Json.Nodes;
using Theseus.App.ViewModels;
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
}
