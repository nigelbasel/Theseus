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

    private HoldoutGuardInstallation Installation() => new(_config);

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
