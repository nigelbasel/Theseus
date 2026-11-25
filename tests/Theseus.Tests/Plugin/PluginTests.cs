using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Theseus.Tests.Plugin;

/// <summary>
/// The agentic-factory plugin, which carries the method to plain Claude Code on any OS (workshop kit, Nigel 2026-11-25).
/// The repo is its own marketplace and the plugin's root, so the hooks run the guards in tools/ and the skills read
/// method/ where they are: nothing is copied, and these tests hold the pieces together.
/// </summary>
[TestFixture]
public class PluginTests
{
    private static readonly string[] Stages = ["research", "spec", "plan", "implement", "verify", "review"];

    private static readonly string[] Commands = ["new-project", "new-workstream", "handoff", "report", "verifier", "check"];

    [Test]
    public void TheMarketplace_ListsThePluginFromTheRepoRoot()
    {
        using var marketplace = Json(".claude-plugin", "marketplace.json");
        var plugin = marketplace.RootElement.GetProperty("plugins").EnumerateArray().Single();

        Assert.That(marketplace.RootElement.GetProperty("name").GetString(), Is.EqualTo("theseus"));
        Assert.That(plugin.GetProperty("name").GetString(), Is.EqualTo("agentic-factory"));
        Assert.That(plugin.GetProperty("source").GetString(), Is.EqualTo("./"));
    }

    [Test]
    public void EveryPathTheManifestAndHooksName_Exists()
    {
        using var manifest = Json(".claude-plugin", "plugin.json");
        using var hooks = Json("plugin", "hooks", "hooks.json");

        var named = manifest.RootElement.GetProperty("skills").EnumerateArray().Select(skill => skill.GetString()!)
            .Append(manifest.RootElement.GetProperty("hooks").GetString()!)
            .Concat(Regex.Matches(hooks.RootElement.GetRawText(), @"\$\{CLAUDE_PLUGIN_ROOT\}/([^""]+)")
                .Select(match => match.Groups[1].Value))
            .ToList();

        Assert.That(named, Has.Count.GreaterThanOrEqualTo(5));
        Assert.That(named.Where(path => !File.Exists(Full(path)) && !Directory.Exists(Full(path))), Is.Empty);
    }

    [Test]
    public void TheGuards_RunOnEveryToolAndBlockWhenTheyCannotRun()
    {
        using var hooks = Json("plugin", "hooks", "hooks.json");
        var guards = hooks.RootElement.GetProperty("hooks").GetProperty("PreToolUse").EnumerateArray().Single();
        var commands = guards.GetProperty("hooks").EnumerateArray().ToList();

        Assert.That(guards.GetProperty("matcher").GetString(), Is.EqualTo("*"));
        Assert.That(commands.Select(hook => hook.GetProperty("args")[0].GetString()), Is.EquivalentTo(new[]
        {
            "${CLAUDE_PLUGIN_ROOT}/tools/guards/trunk-guard.js",
            "${CLAUDE_PLUGIN_ROOT}/tools/holdout/holdout-guard.js",
        }));
        Assert.That(commands.Select(hook => hook.GetProperty("onFailure").GetString()), Is.All.EqualTo("block"));
        Assert.That(commands.Select(hook => hook.GetProperty("command").GetString()), Is.All.EqualTo("node"));
    }

    [Test]
    public void EachStageAndCommand_HasASkillNamedForIt()
    {
        foreach (var skill in Stages.Concat(Commands))
        {
            var text = File.ReadAllText(Full($"plugin/skills/{skill}/SKILL.md"));

            Assert.That(text, Does.StartWith("---"), skill);
            Assert.That(text, Does.Contain($"\nname: {skill}\n").Or.Contain($"\nname: {skill}\r\n"), skill);
            Assert.That(text, Does.Contain("\ndescription: ").Or.Contain("\r\ndescription: "), skill);
        }
    }

    [Test]
    public void EveryMethodFileASkillNames_Exists()
    {
        var named = Directory.EnumerateFiles(Full("plugin/skills"), "SKILL.md", SearchOption.AllDirectories)
            .SelectMany(file => Regex.Matches(File.ReadAllText(file), @"\$\{CLAUDE_PLUGIN_ROOT\}/([^`""\s]+)"))
            .Select(match => match.Groups[1].Value.TrimEnd('/', ')', '.', ','))
            .Distinct()
            .ToList();

        Assert.That(named, Is.Not.Empty);
        Assert.That(named.Where(path => !File.Exists(Full(path)) && !Directory.Exists(Full(path))), Is.Empty);
    }

    [Test]
    public void TheSessionRules_FitClaudeCodesLimitAndCarryBothSections()
    {
        var rules = Node("plugin/scripts/session-rules.js", string.Empty);

        Assert.That(rules.Length, Is.LessThan(10_000), "Claude Code caps a SessionStart hook's context at 10,000 characters");
        Assert.That(rules, Does.Contain("## Rules that hold everywhere"));
        Assert.That(rules, Does.Contain("## Working unattended: decide, record, or park"));
        Assert.That(rules, Does.Contain("Without the Theseus tools"));
    }

    [Test]
    public void ThePublicPluginFiles_NameNoInternalHost()
    {
        var files = Directory.EnumerateFiles(Full("plugin"), "*", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Full(".claude-plugin")))
            .Append(Full("docs/getting-started.md"));
        var internalHost = new Regex(@"nurtur|briefyourmarket|atlassian\.net|tfs\.", RegexOptions.IgnoreCase);

        Assert.That(files.Where(file => internalHost.IsMatch(File.ReadAllText(file))).Select(Path.GetFileName), Is.Empty);
    }

    private static string Node(string script, string input)
    {
        var node = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Select(directory => Path.Combine(directory, "node.exe"))
            .FirstOrDefault(File.Exists);
        if (node is null)
        {
            Assert.Ignore("Node is not installed here.");
        }

        var start = new ProcessStartInfo(node!, $"\"{Full(script)}\"")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(start)!;
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(30_000);
        return output;
    }

    private static JsonDocument Json(params string[] parts) => JsonDocument.Parse(File.ReadAllText(Full(Path.Combine(parts))));

    private static string Full(string relative) => Path.GetFullPath(Path.Combine(RepositoryRoot(), relative));

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
