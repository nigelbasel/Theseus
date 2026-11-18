using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Theseus.Tests.Locks;

/// <summary>
/// The PreToolUse guard under Node, fed the hook input a session's Bash call produces.
/// </summary>
[TestFixture]
public class LockGuardTests
{
    [TestCase("dotnet build Theseus.slnx -c Debug")]
    [TestCase("cd /c/dev/git/Theseus && dotnet test Theseus.slnx --no-build --logger \"console;verbosity=detailed\"")]
    [TestCase("dotnet build C:/dev/git/Theseus/Theseus.slnx")]
    public void TheseusSolution_IsAllowedWithoutTheLock(string command)
    {
        Assert.That(Guard(command), Does.Not.Contain("\"deny\""));
    }

    [TestCase("dotnet build Fabrikam.Messaging.sln")]
    [TestCase("dotnet test CORP.slnf --no-build")]
    [TestCase("dotnet test Fabrikam.Config.Persistence.Tests --no-build")]
    [TestCase("dotnet build Theseus.slnx && dotnet test Other.sln")]
    public void OtherSolutionsAndContainerSuites_AreStillRefused(string command)
    {
        Assert.That(Guard(command), Does.Contain("\"deny\""));
    }

    [TestCase("git commit -m \"Mention ndc-test-lock.sh in a message\"")]
    [TestCase("Select-String -Path tools\\locks\\ndc-test-lock.sh -Pattern 'run'")]
    [TestCase("git log --oneline -- tools/locks/ndc-test-lock.sh")]
    public void APowerShellCommandThatOnlyMentionsTheLockScript_IsAllowed(string command)
    {
        Assert.That(Guard(command, "PowerShell"), Does.Not.Contain("\"deny\""));
    }

    [TestCase("bash ~/.claude/bin/ndc-test-lock.sh run \"me\" \"suite\" -- dotnet test Other.sln")]
    [TestCase("bash \"$HOME/.claude/bin/ndc-test-lock.sh\" wait \"me\" \"suite\"")]
    public void RunningTheLockScriptThroughWslBashFromPowerShell_IsStillRefused(string command)
    {
        Assert.That(Guard(command, "PowerShell"), Does.Contain("\"deny\""));
    }

    [TestCase("& \"C:\\Program Files\\Git\\bin\\bash.exe\" \"$HOME/.claude/bin/ndc-test-lock.sh\" run \"me\" \"suite\" -- dotnet test Other.sln")]
    public void RunningTheLockScriptThroughGitBashFromPowerShell_IsAllowed(string command)
    {
        Assert.That(Guard(command, "PowerShell"), Does.Not.Contain("\"deny\""));
    }

    [TestCase("echo ndc-test-lock.sh && dotnet test Other.sln")]
    [TestCase("grep -n run tools/locks/ndc-test-lock.sh; dotnet build Fabrikam.Messaging.sln")]
    public void MentioningTheLockScript_DoesNotExemptAnUnlockedRunInTheSameCommand(string command)
    {
        Assert.That(Guard(command), Does.Contain("\"deny\""));
    }

    private static string Guard(string command, string tool = "Bash")
    {
        var node = FindNode();
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

    private static string? FindNode() =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Select(directory => Path.Combine(directory, "node.exe"))
            .FirstOrDefault(File.Exists);

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
