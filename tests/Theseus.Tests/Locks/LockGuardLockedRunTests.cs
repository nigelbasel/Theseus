using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Theseus.Tests.Locks;

/// <summary>
/// Correct runs through the lock script are never refused, whatever their own arguments contain. A separator
/// inside quotes belongs to the argument it is in, not to the command line: the first fix for guard-mentions
/// split on it and refused these (found by its verifier, 2026-11-18).
/// </summary>
[TestFixture]
public class LockGuardLockedRunTests
{
    private const string Script = "~/.claude/bin/ndc-test-lock.sh";
    private const string GitBash = "& \"C:\\Program Files\\Git\\bin\\bash.exe\" \"$HOME/.claude/bin/ndc-test-lock.sh\"";

    [TestCase("bash " + Script + " run \"s\" \"suite\" -- dotnet test X.sln --no-build")]
    [TestCase("NDC_TEST_LOCK_WAIT=600 bash " + Script + " run \"s\" \"suite\" -- dotnet test X.Api.Tests")]
    [TestCase("cd /e/x && bash " + Script + " run \"s\" \"suite\" -- dotnet test X.sln")]
    [TestCase("bash " + Script + " run \"s\" \"suite\" -- bash -c \"cd x && dotnet test X.sln\"")]
    [TestCase("bash " + Script + " run \"s\" \"a;b\" -- dotnet test X.sln")]
    [TestCase("bash " + Script + " run --token abc -- dotnet test X.sln")]
    [TestCase("bash " + Script + " run \"s\" \"suite\" -- dotnet test X.sln --filter \"FullyQualifiedName~A|FullyQualifiedName~B\"")]
    [TestCase("bash " + Script + " run \"s\" \"suite\" -- sh -c 'dotnet build X.sln; dotnet test X.sln --no-build'")]
    public void ALockedRunFromBash_IsAllowed(string command)
    {
        Assert.That(Guard(command, "Bash"), Does.Not.Contain("\"deny\""));
    }

    [TestCase(GitBash + " run \"s\" \"suite\" -- dotnet test X.sln")]
    [TestCase("$env:NDC_TEST_LOCK_REASON='x'; " + GitBash + " run \"s\" \"suite\" -- dotnet test X.sln")]
    [TestCase(GitBash + " run \"s\" \"suite\" -- bash -c \"cd /e/x && dotnet test X.sln\"")]
    public void ALockedRunFromPowerShellThroughGitBash_IsAllowed(string command)
    {
        Assert.That(Guard(command, "PowerShell"), Does.Not.Contain("\"deny\""));
    }

    [Test]
    public void AQuotedMentionContainingASeparator_IsNotARun()
    {
        Assert.That(Guard("git commit -m \"docs; bash ndc-test-lock.sh run is the form\"", "PowerShell"),
            Does.Not.Contain("\"deny\""));
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
