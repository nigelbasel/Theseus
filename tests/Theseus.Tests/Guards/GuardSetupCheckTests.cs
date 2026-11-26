using System.Text.Json.Nodes;
using Theseus.Core.Guards;

namespace Theseus.Tests.Guards;

/// <summary>
/// The setup check names any guard missing or out of date, for the board and the first-run setup (guards-ship-with-theseus
/// brief step 2, Nigel 2026-11-26), so a new machine is told what it lacks rather than finding out from a refused push.
/// </summary>
[TestFixture]
public class GuardSetupCheckTests
{
    private string _home = string.Empty;
    private string _config = string.Empty;

    [SetUp]
    public void MakeFixtureFolder()
    {
        _home = Path.Combine(Path.GetTempPath(), "theseus-guard-check-tests", Guid.NewGuid().ToString("N"));
        _config = Path.Combine(_home, ".claude");
        Directory.CreateDirectory(_config);
    }

    [Test]
    public void OnANewMachine_ItNamesEveryGuardAsNotInstalled()
    {
        var findings = Check().Findings();

        Assert.That(findings.Select(finding => finding.Line), Is.EqualTo(new[]
        {
            "The holdout guard is not installed.",
            "The trunk guard is not installed.",
            "The tests-lock guard is not installed.",
        }));
        Assert.That(Check().IsReady, Is.False);
    }

    [Test]
    public void AnOlderGuard_IsNamedAsOutOfDate()
    {
        InstallAllAsTheyShip();
        File.WriteAllText(Path.Combine(_config, "bin", "trunk-guard.js"), "// the November guard");

        Assert.That(Check().Findings().Select(finding => finding.Line), Is.EqualTo(new[] { "The trunk guard is out of date." }));
    }

    [Test]
    public void AGuardClaudeCodeDoesNotRun_IsNamed()
    {
        InstallAllAsTheyShip(register: false);

        Assert.That(Check().Findings().Select(finding => finding.Line), Has.Member("Claude Code's settings do not run the trunk guard."));
    }

    [Test]
    public void WithAllThreeInstalledAndRun_ItNamesNothing()
    {
        InstallAllAsTheyShip();

        Assert.That(Check().Findings(), Is.Empty);
        Assert.That(Check().IsReady, Is.True);
    }

    [Test]
    public void TheBoardsNotice_NamesEachGuard_AndSaysWhereToSetThemUp()
    {
        var notice = GuardSetupCheck.Notice(Check().Findings(), saidFor: null);

        Assert.That(notice, Does.StartWith("Guards to set up in Tools > Options, Guards:"));
        Assert.That(notice, Does.Contain("the trunk guard is not installed").And.Contain("the tests-lock guard is not installed"));
    }

    [Test]
    public void TheBoardsNotice_IsSaidOnceForTheSameFindings_AndAgainWhenTheyChange()
    {
        var findings = Check().Findings();
        var said = GuardSetupCheck.Signature(findings);

        Assert.That(GuardSetupCheck.Notice(findings, said), Is.Null, "already said");

        InstallAllAsTheyShip();
        File.WriteAllText(Path.Combine(_config, "bin", "trunk-guard.js"), "// the November guard");

        Assert.That(GuardSetupCheck.Notice(Check().Findings(), said), Is.EqualTo("Guards to set up in Tools > Options, Guards: the trunk guard is out of date."));
    }

    [Test]
    public void WithNothingToSay_ThereIsNoNotice()
    {
        InstallAllAsTheyShip();

        Assert.That(GuardSetupCheck.Notice(Check().Findings(), saidFor: null), Is.Null);
    }

    [TearDown]
    public void RemoveFixtureFolder()
    {
        if (Directory.Exists(_home))
        {
            Directory.Delete(_home, recursive: true);
        }
    }

    private GuardSetupCheck Check() =>
        new([.. GuardCatalog.All.Select(guard => new GuardInstallation(guard, _config, _home))]);

    // Writes each guard's files as Theseus ships them, without running Node, and registers each hook.
    private void InstallAllAsTheyShip(bool register = true)
    {
        var bin = Path.Combine(_config, "bin");
        Directory.CreateDirectory(bin);
        var entries = new JsonArray();

        foreach (var guard in GuardCatalog.All)
        {
            foreach (var file in guard.Files)
            {
                File.WriteAllText(Path.Combine(bin, file.FileName), file.Text);
            }

            entries.Add(new JsonObject
            {
                ["matcher"] = guard.Matcher,
                ["hooks"] = new JsonArray(new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = $"node \"$HOME/.claude/bin/{guard.Guard.FileName}\" 2>/dev/null",
                }),
            });
        }

        if (register)
        {
            File.WriteAllText(Path.Combine(_config, "settings.json"),
                new JsonObject { ["hooks"] = new JsonObject { ["PreToolUse"] = entries } }.ToJsonString());
        }
    }
}
