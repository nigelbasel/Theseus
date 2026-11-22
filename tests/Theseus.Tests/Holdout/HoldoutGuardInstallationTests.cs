using System.Text.Json.Nodes;
using Theseus.Core.Links;

namespace Theseus.Tests.Holdout;

/// <summary>
/// Install the holdout guard (verifier-runs brief step 3, Nigel 2026-11-22: he should not have to know where the guard
/// lives): Theseus copies its own guard to where the Claude Code hook runs it, keeps the old copy, checks it took on a
/// fixture folder, can put the old copy back, and adds a missing hook entry only once its exact change is shown. Every
/// test works in a fixture .claude folder, never the user's.
/// </summary>
[TestFixture]
public class HoldoutGuardInstallationTests
{
    private string _home = string.Empty;
    private string _config = string.Empty;

    [SetUp]
    public void MakeFixtureFolder()
    {
        _home = Path.Combine(Path.GetTempPath(), "theseus-guard-install-tests", Guid.NewGuid().ToString("N"));
        _config = Path.Combine(_home, ".claude");
        Directory.CreateDirectory(_config);
    }

    [Test]
    public void WithNoGuard_ItSaysNotInstalled_AndThatTheHookIsMissing()
    {
        var state = Installation().State();

        Assert.That(state.IsInstalled, Is.False);
        Assert.That(state.HookIsRegistered, Is.False);
        Assert.That(state.Line, Is.EqualTo("Not installed"));
        Assert.That(state.GuardPath, Is.EqualTo(Path.Combine(_config, "bin", "holdout-guard.js")));
    }

    [Test]
    public void TheGuardGoesWhereTheHookRunsIt()
    {
        WriteSettings("node \"$HOME/.claude/hooks/holdout-guard.js\" 2>/dev/null");

        Assert.That(Installation().State().GuardPath, Is.EqualTo(Path.Combine(_config, "hooks", "holdout-guard.js")));
        Assert.That(Installation().State().HookIsRegistered, Is.True);
    }

    [Test]
    public void Install_CopiesThisTheseussGuard_KeepsTheOldOne_AndChecksItTook()
    {
        RequireNode();
        var guard = Path.Combine(_config, "bin", "holdout-guard.js");
        Directory.CreateDirectory(Path.GetDirectoryName(guard)!);
        File.WriteAllText(guard, "// the 18 November guard");

        var outcome = Installation().Install();

        Assert.That(outcome.Succeeded, Is.True, outcome.Message);
        Assert.That(File.ReadAllText(guard), Is.EqualTo(HoldoutGuardBundle.Text));
        Assert.That(File.ReadAllText(guard + ".previous"), Is.EqualTo("// the 18 November guard"));
        Assert.That(outcome.Message, Does.Contain("refused a read of the holdout by a session with no role"));
        Assert.That(outcome.Message, Does.Contain("allowed a verifier's"));
        Assert.That(Installation().State().Line, Is.EqualTo("Up to date"));
    }

    [Test]
    public void Install_WithNoGuardBefore_KeepsNoOldCopy()
    {
        RequireNode();

        Assert.That(Installation().Install().Succeeded, Is.True);
        Assert.That(File.Exists(Path.Combine(_config, "bin", "holdout-guard.js.previous")), Is.False);
        Assert.That(Installation().State().CanUndo, Is.False);
    }

    [Test]
    public void TheCheck_FailsAGuardThatRefusesNothing()
    {
        RequireNode();
        var installation = Installation();
        installation.Install();
        File.WriteAllText(installation.State().GuardPath, "process.exit(0);");

        var proof = installation.Prove();

        Assert.That(proof.Succeeded, Is.False);
        Assert.That(proof.Message, Does.Contain("did not refuse"));
    }

    [Test]
    public void Undo_PutsTheOldCopyBack()
    {
        RequireNode();
        var guard = Path.Combine(_config, "bin", "holdout-guard.js");
        Directory.CreateDirectory(Path.GetDirectoryName(guard)!);
        File.WriteAllText(guard, "// the 18 November guard");
        var installation = Installation();
        installation.Install();

        var outcome = installation.Undo();

        Assert.That(outcome.Succeeded, Is.True, outcome.Message);
        Assert.That(File.ReadAllText(guard), Is.EqualTo("// the 18 November guard"));
        Assert.That(installation.State().CanUndo, Is.False);
    }

    [Test]
    public void AnOlderGuard_IsOutOfDate_SayingWhenItWasInstalled()
    {
        var guard = Path.Combine(_config, "bin", "holdout-guard.js");
        Directory.CreateDirectory(Path.GetDirectoryName(guard)!);
        File.WriteAllText(guard, "// the 18 November guard");
        File.SetLastWriteTime(guard, new DateTime(2026, 11, 18, 12, 58, 0));

        Assert.That(Installation().State().Line, Is.EqualTo("Installed on 18 November; this Theseus has a newer one"));
    }

    [Test]
    public void TheSameGuardWithOtherLineEndings_IsUpToDate()
    {
        var guard = Path.Combine(_config, "bin", "holdout-guard.js");
        Directory.CreateDirectory(Path.GetDirectoryName(guard)!);
        File.WriteAllText(guard, HoldoutGuardBundle.Text.Replace("\r\n", "\n", StringComparison.Ordinal));

        Assert.That(Installation().State().IsUpToDate, Is.True);
    }

    [Test]
    public void AMissingHookEntry_IsShownBeforeItIsAdded_AndAddedBesideTheOtherHooks()
    {
        File.WriteAllText(Path.Combine(_config, "settings.json"),
            """{ "model": "opus", "hooks": { "PreToolUse": [ { "matcher": "Bash", "hooks": [ { "type": "command", "command": "node lock.js" } ] } ] } }""");
        var installation = Installation();

        var change = installation.HookEntryChange();

        Assert.That(change, Does.Contain("\"matcher\": \"*\""));
        Assert.That(change, Does.Contain("holdout-guard.js"));
        Assert.That(File.ReadAllText(Path.Combine(_config, "settings.json")), Does.Not.Contain("holdout-guard"),
            "showing the change writes nothing");

        Assert.That(installation.AddHookEntry().Succeeded, Is.True);

        var settings = JsonNode.Parse(File.ReadAllText(Path.Combine(_config, "settings.json")))!;
        Assert.That(settings["model"]!.GetValue<string>(), Is.EqualTo("opus"));
        Assert.That(settings["hooks"]!["PreToolUse"]!.AsArray(), Has.Count.EqualTo(2));
        Assert.That(File.Exists(Path.Combine(_config, "settings.json.previous")), Is.True);
        Assert.That(installation.State().HookIsRegistered, Is.True);
    }

    [Test]
    public void WithTheHookRegistered_ThereIsNoChangeToShow()
    {
        WriteSettings("node \"$HOME/.claude/bin/holdout-guard.js\"");

        Assert.That(Installation().HookEntryChange(), Is.Null);
    }

    [Test]
    public void TheOutOfDateNotice_IsGivenOncePerNewGuard_AndNotWhenNoneIsInstalled()
    {
        var guard = Path.Combine(_config, "bin", "holdout-guard.js");

        Assert.That(Installation().State().OutOfDateNotice(null), Is.Null, "nothing installed, nothing out of date");

        Directory.CreateDirectory(Path.GetDirectoryName(guard)!);
        File.WriteAllText(guard, "// the 18 November guard");
        var state = Installation().State();

        Assert.That(state.OutOfDateNotice(null), Is.EqualTo("The holdout guard is out of date: install it in Tools > Options, Holdouts."));
        Assert.That(state.OutOfDateNotice(HoldoutGuardBundle.Version), Is.Null, "already said for this guard");
    }

    [Test]
    public void HooksOfAnUnexpectedShape_AreNeverReplaced()
    {
        const string odd = """{ "hooks": { "PreToolUse": { "matcher": "Bash", "hooks": [ { "type": "command", "command": "node lock.js" } ] } } }""";
        File.WriteAllText(Path.Combine(_config, "settings.json"), odd);

        var outcome = Installation().AddHookEntry();

        Assert.That(outcome.Succeeded, Is.False, "review of the install, M1: the user's hook was deleted");
        Assert.That(File.ReadAllText(Path.Combine(_config, "settings.json")), Is.EqualTo(odd));
    }

    [Test]
    public void TheSettingsStayReadable_AsTheChangeShownDoes()
    {
        File.WriteAllText(Path.Combine(_config, "settings.json"), """{ "statusLine": "it's café > tea & <cake>" }""");
        var installation = Installation();

        var change = installation.HookEntryChange()!;
        installation.AddHookEntry();
        var written = File.ReadAllText(Path.Combine(_config, "settings.json"));

        Assert.That(change, Does.Contain("2>/dev/null").And.Not.Contain(@"\u00"), "review of the install, M2");
        Assert.That(written, Does.Contain("it's café > tea & <cake>").And.Contain("2>/dev/null").And.Not.Contain(@"\u00"));
    }

    [Test]
    public void SettingsWithADuplicateKey_DoNotStopTheseus()
    {
        File.WriteAllText(Path.Combine(_config, "settings.json"), """{ "model": "a", "model": "b" }""");

        Assert.That(() => Installation().State(), Throws.Nothing, "review of the install, M3: Theseus failed to start");
        Assert.That(Installation().AddHookEntry().Succeeded, Is.False, "it does not read as settings, so nothing is written");
    }

    [Test]
    public void InstallingAgainWhenUpToDate_KeepsTheOldCopyForUndo()
    {
        RequireNode();
        var guard = Path.Combine(_config, "bin", "holdout-guard.js");
        Directory.CreateDirectory(Path.GetDirectoryName(guard)!);
        File.WriteAllText(guard, "// the 18 November guard");
        var installation = Installation();

        installation.Install();
        installation.Install();
        installation.Undo();

        Assert.That(File.ReadAllText(guard), Is.EqualTo("// the 18 November guard"), "review of the install, S1");
    }

    [TestCase("node \"$HOME/.claude/my hooks/holdout-guard.js\" 2>/dev/null")]
    [TestCase("node \"${HOME}/.claude/my hooks/holdout-guard.js\"")]
    [TestCase("node \"%USERPROFILE%\\.claude\\my hooks\\holdout-guard.js\"")]
    [TestCase("node '~/.claude/my hooks/holdout-guard.js'")]
    [TestCase("node \"$CLAUDE_CONFIG_DIR/my hooks/holdout-guard.js\"")]
    public void TheGuardPath_IsReadAsTheHookRunsIt(string command)
    {
        WriteSettings(command);

        Assert.That(Installation().State().GuardPath, Is.EqualTo(Path.Combine(_config, "my hooks", "holdout-guard.js")),
            "review of the install, S2");
    }

    [Test]
    public void AGitBashGuardPath_IsTheWindowsPath()
    {
        var windows = Path.Combine(_config, "bin", "holdout-guard.js");
        WriteSettings($"node \"/{char.ToLowerInvariant(windows[0])}{windows[2..].Replace('\\', '/')}\"");

        Assert.That(Installation().State().GuardPath, Is.EqualTo(windows), "review of the install, S2");
    }

    [Test]
    public void ABackupOfTheGuard_IsNotTheGuard()
    {
        WriteSettings("node \"$HOME/.claude/bin/holdout-guard.js.bak\"");

        Assert.That(Installation().State().HookIsRegistered, Is.False, "review of the install, S2");
    }

    [Test]
    public void AGuardRunFromOutsideTheClaudeFolder_IsNotOverwritten()
    {
        var repoCopy = Path.Combine(_home, "src", "Theseus", "tools", "holdout", "holdout-guard.js");
        Directory.CreateDirectory(Path.GetDirectoryName(repoCopy)!);
        File.WriteAllText(repoCopy, "// tracked in a repo");
        WriteSettings($"node \"{repoCopy.Replace('\\', '/')}\"");

        var outcome = Installation().Install();

        Assert.That(outcome.Succeeded, Is.False, "review of the install, S3: it overwrote a file a repo tracks");
        Assert.That(outcome.Message, Does.Contain("outside Claude Code's folder"));
        Assert.That(File.ReadAllText(repoCopy), Is.EqualTo("// tracked in a repo"));
    }

    [Test]
    public void AHookForSomeToolsOnly_IsNotTheGuardRegistered()
    {
        File.WriteAllText(Path.Combine(_config, "settings.json"),
            """{ "hooks": { "PreToolUse": [ { "matcher": "Bash", "hooks": [ { "type": "command", "command": "node \"$HOME/.claude/bin/holdout-guard.js\"" } ] } ] } }""");

        Assert.That(Installation().State().HookIsRegistered, Is.False, "review of the install, S6: it must run before every tool");
    }

    [Test]
    public void AGuardThatHangs_FailsTheCheck_InsteadOfHangingTheseus()
    {
        RequireNode();
        var guard = Path.Combine(_home, "hanging.js");
        File.WriteAllText(guard, "setInterval(() => {}, 1000);");

        var proof = HoldoutGuardProof.Run(guard, TimeSpan.FromSeconds(2));

        Assert.That(proof.Succeeded, Is.False, "review of the install, S5");
        Assert.That(proof.Message, Does.Contain("did not answer"));
    }

    [Test]
    public void ARelativeConfigFolder_DoesNotStopTheseus()
    {
        Assert.That(() => new HoldoutGuardInstallation(".claude-relative", _home).State(), Throws.Nothing,
            "review of the install, consider: .First() threw");
    }

    [TearDown]
    public void RemoveFixtureFolder()
    {
        if (Directory.Exists(_home))
        {
            Directory.Delete(_home, recursive: true);
        }
    }

    private HoldoutGuardInstallation Installation() => new(_config, _home);

    private void WriteSettings(string command) =>
        File.WriteAllText(Path.Combine(_config, "settings.json"), new JsonObject
        {
            ["hooks"] = new JsonObject
            {
                ["PreToolUse"] = new JsonArray(new JsonObject
                {
                    ["matcher"] = "*",
                    ["hooks"] = new JsonArray(new JsonObject { ["type"] = "command", ["command"] = command }),
                }),
            },
        }.ToJsonString());

    private static void RequireNode()
    {
        if (HoldoutGuardProof.FindNode() is null)
        {
            Assert.Ignore("Node is not installed here.");
        }
    }
}
