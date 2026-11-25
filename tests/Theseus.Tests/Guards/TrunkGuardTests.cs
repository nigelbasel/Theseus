using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Theseus.Tests.Guards;

/// <summary>
/// The trunk guard under Node, fed the hook input a session's tool call produces: what only the owner does to a trunk and
/// its pull requests is refused, deleting a branch or force-pushing asks him, and everything else passes (factory
/// blockers, Nigel 2026-11-25).
/// </summary>
[TestFixture]
public class TrunkGuardTests
{
    [TestCase("git push origin main")]
    [TestCase("git push origin HEAD:main")]
    [TestCase("git push origin nigel/x:refs/heads/master")]
    [TestCase("git -C E:/work/x/Repo push --force origin trunk")]
    [TestCase("git push --mirror origin")]
    [TestCase("cd repo && git push -u origin HEAD:Main")]
    public void APushToTheTrunk_IsRefused(string command)
    {
        Assert.That(Run("Bash", command), Is.EqualTo("deny"));
    }

    [TestCase("gh pr merge 12 --squash")]
    [TestCase("gh pr review 12 --approve")]
    [TestCase("gh api repos/o/r/pulls/12/merge -X PUT")]
    [TestCase("az repos pr update --id 23128 --status completed")]
    [TestCase("az repos pr update --id 23128 --status abandoned")]
    [TestCase("az repos pr update --id 23128 --auto-complete true")]
    [TestCase("az repos pr update --id 23128 --bypass-policy true")]
    [TestCase("az repos pr set-vote --id 23128 --vote approve")]
    [TestCase("curl -X PATCH https://tfs.example/_apis/git/repositories/r/pullRequests/23128 -d '{\"status\":\"completed\"}'")]
    [TestCase("curl -X PATCH https://tfs.example/_apis/git/repositories/r/pullrequests/23128 -d '{\"autoCompleteSetBy\":{\"id\":\"x\"}}'")]
    public void CompletingApprovingOrAbandoningAPullRequest_IsRefused(string command)
    {
        Assert.That(Run("Bash", command), Is.EqualTo("deny"));
    }

    [TestCase("mcp__github__merge_pull_request")]
    [TestCase("mcp__ado__complete_pull_request")]
    [TestCase("mcp__ado__pull_request_approve")]
    public void AnMcpToolThatMergesOrApprovesAPullRequest_IsRefused(string tool)
    {
        Assert.That(Run(tool, "{}", field: "body"), Is.EqualTo("deny"));
    }

    [TestCase("git push --force origin nigel/fix-nuf-1436")]
    [TestCase("git push -f origin nigel/fix-nuf-1436")]
    [TestCase("git push --force-with-lease origin nigel/fix-nuf-1436")]
    [TestCase("git push origin +nigel/fix-nuf-1436")]
    [TestCase("git push origin --delete nigel/fix-nuf-1436")]
    [TestCase("git push origin :nigel/fix-nuf-1436")]
    [TestCase("git branch -D nigel/fix-nuf-1436")]
    [TestCase("git branch --delete nigel/old")]
    public void ForcePushingOrDeletingABranch_AsksTheOwner(string command)
    {
        Assert.That(Run("Bash", command), Is.EqualTo("ask"));
    }

    [TestCase("git push origin nigel/fix-nuf-1436")]
    [TestCase("git push -u origin HEAD:nigel/fix-nuf-1436")]
    [TestCase("git push origin HEAD:nigel/verifier-card HEAD:nigel/theseus")]
    [TestCase("git push --dry-run origin nigel/bug-fixing")]
    [TestCase("git branch -vv")]
    [TestCase("git log --oneline main..HEAD")]
    [TestCase("gh pr create --base nigel/theseus --title x")]
    [TestCase("gh pr view 12")]
    [TestCase("az repos pr create --target-branch main --title x")]
    [TestCase("az repos pr update --id 23128 --title better")]
    [TestCase("curl https://tfs.example/_apis/git/repositories/r/pullRequests/23128")]
    public void OrdinaryWork_Passes(string command)
    {
        Assert.That(Run("Bash", command), Is.Null);
    }

    [Test]
    public void APowerShellPushToTheTrunk_IsRefusedToo()
    {
        Assert.That(Run("PowerShell", "git push origin main; if ($?) { Write-Output ok }"), Is.EqualTo("deny"));
    }

    [Test]
    public void APushWithNoRefspec_FromABranchOnTheTrunk_IsRefused_AndFromAWorkstreamBranch_Passes()
    {
        var repo = Path.Combine(Path.GetTempPath(), "theseus-trunk-guard-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        Git(repo, "init", "-q", "-b", "main");

        Assert.That(Run("Bash", "git push", cwd: repo), Is.EqualTo("deny"));
        Assert.That(Run("Bash", $"git -C \"{repo}\" push origin", cwd: Path.GetTempPath()), Is.EqualTo("deny"));

        Git(repo, "checkout", "-q", "-b", "nigel/work");

        Assert.That(Run("Bash", "git push", cwd: repo), Is.Null);
    }

    private static void Git(string folder, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = folder, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        process.WaitForExit(30_000);
    }

    // The decision the guard gave, "deny" or "ask", or null when it let the call through.
    private static string? Run(string tool, string value, string field = "command", string? cwd = null)
    {
        var node = FindNode();
        if (node is null)
        {
            Assert.Ignore("Node is not installed here.");
        }

        var script = Path.Combine(RepositoryRoot(), "tools", "guards", "trunk-guard.js");
        var start = new ProcessStartInfo(node!, $"\"{script}\"")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(start)!;
        process.StandardInput.Write(JsonSerializer.Serialize(new
        {
            tool_name = tool,
            tool_input = new Dictionary<string, string> { [field] = value },
            cwd = cwd ?? Path.GetTempPath(),
        }));
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(30_000);

        if (output.Length == 0)
        {
            return null;
        }

        using var decision = JsonDocument.Parse(output);
        return decision.RootElement.GetProperty("hookSpecificOutput").GetProperty("permissionDecision").GetString();
    }

    private static string? FindNode() =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Select(directory => Path.Combine(directory, "node.exe"))
            .FirstOrDefault(File.Exists);

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
