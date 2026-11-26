using System.Text.Json.Nodes;
using Theseus.Core.Setup;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Setup;

/// <summary>
/// Registering the stop hook from the setup: the exact entry is shown first, and Claude Code's settings are written only when
/// the owner agrees (first-run-setup brief step 1, as Options › Holdouts adds the guard's entry).
/// </summary>
[TestFixture]
public class StopHookRegistrationTests
{
    private const string Hook = @"C:\Program Files\Theseus\Theseus.Hook.exe";
    private string _config = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _config = Path.Combine(Path.GetTempPath(), "theseus-stop-hook-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_config);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_config, recursive: true);

    private string SettingsFile => Path.Combine(_config, "settings.json");

    [Test]
    public void TheChange_NamesTheFile_AndTheQuotedHookOnBothEvents()
    {
        var change = new StopHookRegistration(_config, Hook).Change();

        Assert.That(change, Does.Contain(SettingsFile).And.Contain("\"Stop\"").And.Contain("\"UserPromptSubmit\""));
        Assert.That(change, Does.Contain("\"\\\"C:\\\\Program Files\\\\Theseus\\\\Theseus.Hook.exe\\\"\""), "quoted, so a shell reads one path");
    }

    [Test]
    public void Adding_WithNoSettingsFile_WritesOneThatRegistersTheHook()
    {
        var registration = new StopHookRegistration(_config, Hook);

        var outcome = registration.Add();

        Assert.That(outcome.Succeeded, Is.True, outcome.Message);
        Assert.That(registration.IsRegistered, Is.True);
        Assert.That(HookInstallation.IsInstalled(File.ReadAllText(SettingsFile)), Is.True);
    }

    [Test]
    public void Adding_KeepsEverythingElse_AndTheOldFileBesideIt()
    {
        const string before = """
            { "model": "opus", "hooks": { "Stop": [ { "hooks": [ { "type": "command", "command": "other-stop" } ] } ],
              "PreToolUse": [ { "matcher": "*", "hooks": [ { "type": "command", "command": "guard" } ] } ] } }
            """;
        File.WriteAllText(SettingsFile, before);

        var outcome = new StopHookRegistration(_config, Hook).Add();

        var settings = JsonNode.Parse(File.ReadAllText(SettingsFile))!;
        Assert.That(outcome.Succeeded, Is.True, outcome.Message);
        Assert.That((string?)settings["model"], Is.EqualTo("opus"));
        Assert.That(settings["hooks"]!["Stop"]!.AsArray(), Has.Count.EqualTo(2));
        Assert.That(settings["hooks"]!["UserPromptSubmit"]!.AsArray(), Has.Count.EqualTo(1));
        Assert.That(settings["hooks"]!["PreToolUse"]!.AsArray(), Has.Count.EqualTo(1));
        Assert.That(File.ReadAllText(SettingsFile + ".previous"), Is.EqualTo(before));
    }

    [Test]
    public void Adding_ToSettingsItCannotSafelyRewrite_ChangesNothing()
    {
        const string odd = """{ "hooks": { "Stop": "not a list" } }""";
        File.WriteAllText(SettingsFile, odd);
        var registration = new StopHookRegistration(_config, Hook);

        Assert.That(registration.Change(), Is.Null);
        Assert.That(registration.Add().Succeeded, Is.False);
        Assert.That(File.ReadAllText(SettingsFile), Is.EqualTo(odd));
    }

    [Test]
    public void AHookAlreadyRegistered_HasNoChangeToMake()
    {
        File.WriteAllText(SettingsFile, """{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"D:/x/Theseus.Hook.exe"}]}]}}""");

        var registration = new StopHookRegistration(_config, Hook);

        Assert.That((registration.IsRegistered, registration.Change()), Is.EqualTo((true, (string?)null)));
    }
}
