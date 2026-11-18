using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Theseus.Tests.Locks;

/// <summary>
/// The guard honours quotes only when it can be sure how the shell reads them, and otherwise refuses rather
/// than allows (guard-mentions, fix pass 3, Nigel's option A, 2026-11-18). Each refused case is a command whose
/// unlocked run really executed in its shell when the second verifier tried it against fix pass 2.
/// </summary>
[TestFixture]
public class LockGuardUnsureQuotingTests
{
    private const string Script = "~/.claude/bin/ndc-test-lock.sh";
    private const string GitBash = "& \"C:\\Program Files\\Git\\bin\\bash.exe\" \"$HOME/.claude/bin/ndc-test-lock.sh\"";
    private const string Unlocked = "dotnet test X.sln";

    [TestCase("bash " + Script + " run a b -- echo \\\" ; " + Unlocked + " ; echo \\\"")]
    [TestCase("bash " + Script + " run a b -- echo it\\'s; " + Unlocked)]
    [TestCase("bash " + Script + " run a b -- true # \"\n" + Unlocked)]
    [TestCase("bash " + Script + " run a b -- true # it's\n" + Unlocked)]
    [TestCase("bash " + Script + " run a b -- echo $'it\\'s' ; " + Unlocked)]
    [TestCase("bash " + Script + " run a b -- echo \\' ; " + Unlocked + " ; echo \\'")]
    [TestCase("bash " + Script + " run a b -- echo \"a\" \\\"\ndocker compose build api")]
    [TestCase("\"" + Script + "\" run a b -- echo \\\"; " + Unlocked)]
    public void AnUnlockedRunHiddenByQuotingFromBash_IsRefused(string command)
    {
        Assert.That(Guard(command, "Bash"), Does.Contain("\"deny\""));
    }

    [TestCase(GitBash + " run a b -- echo \"x\\\" ; " + Unlocked + " ; \"y\"")]
    [TestCase(GitBash + " run a b -- echo `\" ; " + Unlocked)]
    [TestCase(GitBash + " status `\" ; bash " + Script + " run a b -- " + Unlocked)]
    [TestCase(GitBash + " run a b -- Write-Output \"a`\"b\" ; " + Unlocked)]
    public void AnUnlockedRunHiddenByQuotingFromPowerShell_IsRefused(string command)
    {
        Assert.That(Guard(command, "PowerShell"), Does.Contain("\"deny\""));
    }

    [TestCase("NDC_TEST_LOCK_REASON=\"fix && verify\" bash " + Script + " run \"s\" \"suite\" -- " + Unlocked)]
    [TestCase("NDC_TEST_LOCK_REASON=\"two words\" bash " + Script + " run \"s\" \"suite\" -- " + Unlocked)]
    [TestCase("NDC_TEST_LOCK_REASON='two words' NDC_TEST_LOCK_WAIT=600 bash " + Script + " run \"s\" \"suite\" -- " + Unlocked)]
    public void ALockedRunWithAQuotedReason_IsAllowed(string command)
    {
        Assert.That(Guard(command, "Bash"), Does.Not.Contain("\"deny\""));
    }

    private static string Guard(string command, string tool)
    {
        var node = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Select(directory => Path.Combine(directory, "node.exe"))
            .FirstOrDefault(File.Exists);
        if (node is null)
        {
            Assert.Ignore("Node is not installed here.");
        }

        var script = Path.Combine(RepositoryRoot(), "tools", "locks", "ndc-test-lock-guard.js");
        var start = new ProcessStartInfo(node!, $"\"{script}\"")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        start.Environment["THESEUS_LOCKS_URL"] = "http://localhost:1/locks";

        using var process = Process.Start(start)!;
        process.StandardInput.Write(JsonSerializer.Serialize(new { tool_name = tool, tool_input = new { command } }));
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(30_000);

        return output;
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
