using System.Text.Json.Nodes;
using Theseus.Core.Guards;
using Theseus.Core.Links;

namespace Theseus.Tests.Guards;

/// <summary>
/// The trunk and tests-lock guards ship with Theseus and are installed from Options the way the holdout guard is
/// (guards-ship-with-theseus brief step 1, Nigel 2026-11-26): embedded in the build, checked on a fixture, the previous
/// copy kept, and a missing hook entry added only once its exact change is shown. Every test works in a fixture .claude
/// folder, never the user's.
/// </summary>
[TestFixture]
public class GuardInstallationTests
{
    private string _home = string.Empty;
    private string _config = string.Empty;

    [SetUp]
    public void MakeFixtureFolder()
    {
        _home = Path.Combine(Path.GetTempPath(), "theseus-guards-tests", Guid.NewGuid().ToString("N"));
        _config = Path.Combine(_home, ".claude");
        Directory.CreateDirectory(_config);
    }

    [Test]
    public void ANewMachine_HasAllThreeGuardsToInstall()
    {
        Assert.That(GuardCatalog.All.Select(guard => guard.Name),
            Is.EqualTo(new[] { "holdout guard", "trunk guard", "tests-lock guard" }));
    }

    [Test]
    public void EachGuardShipped_IsTheOneInTheRepository()
    {
        foreach (var (guard, file) in new[]
                 {
                     (GuardCatalog.Holdout.Guard, Path.Combine("tools", "holdout", "holdout-guard.js")),
                     (GuardCatalog.Trunk.Guard, Path.Combine("tools", "guards", "trunk-guard.js")),
                     (GuardCatalog.TestsLock.Guard, Path.Combine("tools", "locks", "ndc-test-lock-guard.js")),
                     (GuardCatalog.TestsLock.Companions[0], Path.Combine("tools", "locks", "ndc-test-lock.sh")),
                 })
        {
            Assert.That(guard.Version, Is.EqualTo(GuardFile.VersionOf(File.ReadAllText(Path.Combine(RepositoryRoot(), file)))), file);
        }
    }

    [Test]
    public void TheTrunkGuard_WithNoneInstalled_IsNotInstalled_AndGoesInBin()
    {
        var state = Trunk().State();

        Assert.That(state.IsInstalled, Is.False);
        Assert.That(state.HookIsRegistered, Is.False);
        Assert.That(state.Line, Is.EqualTo("Not installed"));
        Assert.That(state.GuardPath, Is.EqualTo(Path.Combine(_config, "bin", "trunk-guard.js")));
    }

    [Test]
    public void InstallingTheTrunkGuard_CopiesIt_AndTheCheckRefusesAPushToMain_AndPassesABranchPush()
    {
        RequireNode();

        var outcome = Trunk().Install();

        Assert.That(outcome.Succeeded, Is.True, outcome.Message);
        Assert.That(File.ReadAllText(Path.Combine(_config, "bin", "trunk-guard.js")), Is.EqualTo(GuardCatalog.Trunk.Guard.Text));
        Assert.That(outcome.Message, Does.Contain("refused a push to main").And.Contain("passed a push of a workstream branch"));
        Assert.That(Trunk().State().Line, Is.EqualTo("Up to date"));
    }

    [Test]
    public void TheTrunkGuardsCheck_FailsAGuardThatRefusesNothing()
    {
        RequireNode();
        var installation = Trunk();
        installation.Install();
        File.WriteAllText(installation.State().GuardPath, "process.exit(0);");

        var proof = installation.Prove();

        Assert.That(proof.Succeeded, Is.False);
        Assert.That(proof.Message, Does.Contain("did not refuse a push to main"));
    }

    [Test]
    public void TheTrunkGuardsCheck_FailsAGuardThatRefusesEverything()
    {
        RequireNode();
        var installation = Trunk();
        installation.Install();
        File.WriteAllText(installation.State().GuardPath,
            "process.stdout.write(JSON.stringify({hookSpecificOutput:{hookEventName:'PreToolUse',permissionDecision:'deny',permissionDecisionReason:'no'}}));");

        var proof = installation.Prove();

        Assert.That(proof.Succeeded, Is.False);
        Assert.That(proof.Message, Does.Contain("did not pass a push of a workstream branch"));
    }

    [Test]
    public void TheTrunkGuardsHookEntry_RunsItBeforeEveryTool_AndIsShownBeforeItIsAdded()
    {
        var installation = Trunk();

        var change = installation.HookEntryChange();

        Assert.That(change, Does.Contain("\"matcher\": \"*\"").And.Contain("trunk-guard.js"));
        Assert.That(File.Exists(Path.Combine(_config, "settings.json")), Is.False, "showing the change writes nothing");

        Assert.That(installation.AddHookEntry().Succeeded, Is.True);
        Assert.That(installation.State().HookIsRegistered, Is.True);
    }

    [Test]
    public void TheHoldoutGuardsHook_IsNotTakenForTheTrunkGuard()
    {
        WriteSettings("*", "node \"$HOME/.claude/bin/holdout-guard.js\" 2>/dev/null");

        Assert.That(Trunk().State().HookIsRegistered, Is.False);
        Assert.That(Trunk().State().GuardPath, Is.EqualTo(Path.Combine(_config, "bin", "trunk-guard.js")));
    }

    [Test]
    public void AGuardWhoseNameOnlyEndsTheSame_IsNotTheGuard()
    {
        WriteSettings("*", "node \"$HOME/.claude/bin/my-trunk-guard.js\"");

        Assert.That(Trunk().State().HookIsRegistered, Is.False);
    }

    [Test]
    public void InstallingTheTestsLockGuard_PutsTheLockScriptBesideIt_AndChecksItRefusesAnUnlockedRun()
    {
        RequireNode();

        var outcome = TestsLock().Install();

        Assert.That(outcome.Succeeded, Is.True, outcome.Message);
        Assert.That(File.ReadAllText(Path.Combine(_config, "bin", "ndc-test-lock.sh")), Is.EqualTo(GuardCatalog.TestsLock.Companions[0].Text));
        Assert.That(File.ReadAllText(Path.Combine(_config, "bin", "ndc-test-lock.sh")), Does.Not.Contain("\r\n"),
            "bash reads a carriage return as part of each command");
        Assert.That(outcome.Message, Does.Contain("refused a container-backed test run outside the lock").And.Contain("passed a unit test run"));
        Assert.That(TestsLock().State().IsUpToDate, Is.True);
    }

    [Test]
    public void ATestsLockGuardWithAnOldLockScript_IsOutOfDate_AndInstallingKeepsTheOldScript()
    {
        RequireNode();
        var installation = TestsLock();
        installation.Install();
        var script = Path.Combine(_config, "bin", "ndc-test-lock.sh");
        File.WriteAllText(script, "# the October script");

        Assert.That(installation.State().IsUpToDate, Is.False);

        installation.Install();

        Assert.That(File.ReadAllText(script + ".previous"), Is.EqualTo("# the October script"));
        Assert.That(installation.State().IsUpToDate, Is.True);
    }

    [Test]
    public void UndoingTheTestsLockGuard_PutsBackTheGuardAndTheScript()
    {
        RequireNode();
        var bin = Path.Combine(_config, "bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "ndc-test-lock-guard.js"), "// the October guard");
        File.WriteAllText(Path.Combine(bin, "ndc-test-lock.sh"), "# the October script");
        var installation = TestsLock();
        installation.Install();

        var outcome = installation.Undo();

        Assert.That(outcome.Succeeded, Is.True, outcome.Message);
        Assert.That(File.ReadAllText(Path.Combine(bin, "ndc-test-lock-guard.js")), Is.EqualTo("// the October guard"));
        Assert.That(File.ReadAllText(Path.Combine(bin, "ndc-test-lock.sh")), Is.EqualTo("# the October script"));
    }

    [Test]
    public void TheTestsLockGuard_RunForBashAndPowerShell_IsRegistered()
    {
        WriteSettings("Bash|PowerShell", "node \"$HOME/.claude/bin/ndc-test-lock-guard.js\" 2>/dev/null");

        Assert.That(TestsLock().State().HookIsRegistered, Is.True);
    }

    [Test]
    public void TheTestsLockGuard_RunForBashAlone_IsNotRegistered()
    {
        WriteSettings("Bash", "node \"$HOME/.claude/bin/ndc-test-lock-guard.js\" 2>/dev/null");

        Assert.That(TestsLock().State().HookIsRegistered, Is.False, "a PowerShell test run would go unchecked");
    }

    [Test]
    public void TheTestsLockGuardsHookEntry_IsForBashAndPowerShell()
    {
        Assert.That(TestsLock().HookEntryChange(), Does.Contain("\"matcher\": \"Bash|PowerShell\"").And.Contain("ndc-test-lock-guard.js"));
    }

    [Test]
    public void AMatcherThatIsNotARegularExpression_DoesNotStopTheseus()
    {
        WriteSettings("Bash|(", "node \"$HOME/.claude/bin/ndc-test-lock-guard.js\"");

        Assert.That(() => TestsLock().State(), Throws.Nothing);
        Assert.That(TestsLock().State().HookIsRegistered, Is.False);
    }

    [Test]
    public void AddingTheTrunkGuardsEntry_KeepsTheHoldoutGuardsEntry()
    {
        WriteSettings("*", "node \"$HOME/.claude/bin/holdout-guard.js\" 2>/dev/null");

        Trunk().AddHookEntry();

        var entries = JsonNode.Parse(File.ReadAllText(Path.Combine(_config, "settings.json")))!["hooks"]!["PreToolUse"]!.AsArray();
        Assert.That(entries, Has.Count.EqualTo(2));
        Assert.That(new GuardInstallation(GuardCatalog.Holdout, _config, _home).State().HookIsRegistered, Is.True);
    }

    [TearDown]
    public void RemoveFixtureFolder()
    {
        if (Directory.Exists(_home))
        {
            Directory.Delete(_home, recursive: true);
        }
    }

    private GuardInstallation Trunk() => new(GuardCatalog.Trunk, _config, _home);

    private GuardInstallation TestsLock() => new(GuardCatalog.TestsLock, _config, _home);

    private void WriteSettings(string matcher, string command) =>
        File.WriteAllText(Path.Combine(_config, "settings.json"), new JsonObject
        {
            ["hooks"] = new JsonObject
            {
                ["PreToolUse"] = new JsonArray(new JsonObject
                {
                    ["matcher"] = matcher,
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

    private static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
