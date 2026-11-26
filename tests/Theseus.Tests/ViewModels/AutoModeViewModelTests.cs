using Theseus.App.ViewModels;
using Theseus.Core.Guards;
using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Options, Guards, auto mode's environment: the owner's entries are kept in his Theseus settings, and Claude Code's
/// settings are written only when he agrees to the change shown (guards-ship-with-theseus brief step 3, Nigel 2026-11-26).
/// </summary>
[TestFixture]
public class AutoModeViewModelTests
{
    private string _folder = string.Empty;

    [SetUp]
    public void MakeFixtureFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), "theseus-automode-vm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [Test]
    public void AnEntry_IsKeptInTheseusSettings_AndWritesNothingToClaudeCode()
    {
        var section = Section();

        section.SetSourceControlHosts("git.example.com, github.com/someone ,");

        Assert.That(new SettingsStore(TheseusSettingsPath).Read().AutoModeSourceControlHosts, Is.EqualTo(new[] { "git.example.com", "github.com/someone" }));
        Assert.That(section.SourceControlHostsValue, Is.EqualTo("git.example.com, github.com/someone"));
        Assert.That(File.Exists(ClaudeSettingsPath), Is.False);
        Assert.That(section.Line, Is.EqualTo("Not written"));
    }

    [Test]
    public void Write_ShowsTheChange_AndDeclinedWritesNothing()
    {
        var section = Section();
        string? shown = null;
        section.ConfirmChange = change =>
        {
            shown = change;

            return false;
        };
        section.SetSourceControlHosts("git.example.com");

        section.WriteCommand.Execute(null);

        Assert.That(shown, Does.Contain("git.example.com"));
        Assert.That(File.Exists(ClaudeSettingsPath), Is.False);
        Assert.That(section.Result, Is.EqualTo("Nothing was changed."));
    }

    [Test]
    public void Write_Agreed_WritesTheEnvironment()
    {
        var section = Section();
        section.ConfirmChange = _ => true;
        section.SetInternalDomains("git.example.com");

        section.WriteCommand.Execute(null);

        Assert.That(File.ReadAllText(ClaudeSettingsPath), Does.Contain("**Trusted internal domains**: git.example.com."));
        Assert.That(section.Line, Is.EqualTo("Written; it matches your entries"));
    }

    [Test]
    public void WithNoWayToAsk_NothingIsWritten()
    {
        var section = Section();

        section.WriteCommand.Execute(null);

        Assert.That(File.Exists(ClaudeSettingsPath), Is.False);
    }

    [TearDown]
    public void RemoveFixtureFolder()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string TheseusSettingsPath => Path.Combine(_folder, "theseus-settings.json");

    private string ClaudeSettingsPath => Path.Combine(_folder, ".claude", "settings.json");

    private AutoModeViewModel Section() =>
        new(new SettingsStore(TheseusSettingsPath), new AutoModeEnvironment(Path.Combine(_folder, ".claude")));
}
