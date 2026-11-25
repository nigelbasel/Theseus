using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Theseus.Tests.Holdout;

/// <summary>
/// The holdout guard under Node, fed the hook input a session's tool call produces. By role: no role may not touch
/// the holdout; a verifier may read it and write only in its results folder, and may write nothing outside it; an
/// author may do anything. With no holdout set, it allows everything (verifier-card AC 4 to 6). A tripwire for
/// accidents, like the lock guard.
/// </summary>
[TestFixture]
public class HoldoutGuardTests
{
    private const string Folder = @"C:\holdout";
    private const string Remote = "example.com/owner/holdout";
    private const string Scenario = @"C:\holdout\scenarios\stop.md";
    private const string Result = @"C:\holdout\results\close-step-20261119-1800.md";
    private const string Code = @"C:\work\repo\src\Program.cs";

    private string _settingsWithHoldout = string.Empty;
    private string _settingsWithout = string.Empty;

    [OneTimeSetUp]
    public void WriteSettings()
    {
        var folder = Path.Combine(Path.GetTempPath(), "theseus-holdout-guard-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        _settingsWithHoldout = Path.Combine(folder, "with.json");
        File.WriteAllText(_settingsWithHoldout,
            JsonSerializer.Serialize(new { holdoutFolder = Folder, holdoutRemote = Remote }));

        _settingsWithout = Path.Combine(folder, "without.json");
        File.WriteAllText(_settingsWithout, "{}");
    }

    [TestCase("Read", "file_path", Scenario)]
    [TestCase("Grep", "path", Folder)]
    [TestCase("Bash", "command", "cat C:/holdout/scenarios/stop.md")]
    [TestCase("WebFetch", "url", "https://example.com/owner/holdout/blob/main/scenarios/stop.md")]
    [TestCase("Read", "file_path", @"c:\HOLDOUT\scenarios\stop.md")]
    public void NoRole_IsRefusedTheHoldout(string tool, string field, string value)
    {
        Assert.That(Guard(tool, field, value, role: null), Is.True);
    }

    [TestCase("Read", "file_path", Code)]
    [TestCase("Write", "file_path", Code)]
    [TestCase("Read", "file_path", @"C:\holdout-notes\other.md")]
    public void NoRole_IsNotRefusedAnythingElse(string tool, string field, string value)
    {
        Assert.That(Guard(tool, field, value, role: null), Is.False);
    }

    [TestCase("Read", "file_path", Scenario)]
    [TestCase("Grep", "path", Folder)]
    [TestCase("Glob", "pattern", @"C:\holdout\scenarios\*.md")]
    [TestCase("Write", "file_path", Result)]
    [TestCase("Edit", "file_path", Result)]
    public void AVerifier_MayReadTheHoldoutAndWriteItsResults(string tool, string field, string value)
    {
        Assert.That(Guard(tool, field, value, role: "verifier"), Is.False);
    }

    [TestCase("Edit", "file_path", Scenario)]
    [TestCase("Write", "file_path", Scenario)]
    [TestCase("Bash", "command", "git -C C:/holdout commit -am x")]
    public void AVerifier_MayNotChangeTheScenarios(string tool, string field, string value)
    {
        Assert.That(Guard(tool, field, value, role: "verifier"), Is.True);
    }

    [TestCase("Write", "file_path", Code)]
    [TestCase("Edit", "file_path", Code)]
    [TestCase("NotebookEdit", "notebook_path", @"C:\work\repo\analysis.ipynb")]
    public void AVerifier_MayNotWriteOutsideItsResults(string tool, string field, string value)
    {
        Assert.That(Guard(tool, field, value, role: "verifier"), Is.True);
    }

    [TestCase("Read", "file_path", Code)]
    [TestCase("Bash", "command", "dotnet test Theseus.slnx")]
    public void AVerifier_MayStillReadAndRunTheCode(string tool, string field, string value)
    {
        Assert.That(Guard(tool, field, value, role: "verifier"), Is.False);
    }

    [Test]
    public void AnAuthor_MayChangeTheScenarios()
    {
        Assert.That(Guard("Edit", "file_path", Scenario, role: null, author: true), Is.False);
    }

    [Test]
    public void WithNoHoldoutSet_NothingIsRefused()
    {
        Assert.That(Guard("Read", "file_path", Scenario, role: null, settings: _settingsWithout), Is.False);
    }

    private bool Guard(string tool, string field, string value, string? role, bool author = false, string? settings = null)
    {
        var node = FindNode();
        if (node is null)
        {
            Assert.Ignore("Node is not installed here.");
        }

        var script = Path.Combine(RepositoryRoot(), "tools", "holdout", "holdout-guard.js");
        var start = new ProcessStartInfo(node!, $"\"{script}\"")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        start.Environment["THESEUS_SETTINGS"] = settings ?? _settingsWithHoldout;
        start.Environment.Remove("THESEUS_ROLE");
        start.Environment.Remove("THESEUS_HOLDOUT_AUTHOR");

        if (role is not null)
        {
            start.Environment["THESEUS_ROLE"] = role;
        }

        if (author)
        {
            start.Environment["THESEUS_HOLDOUT_AUTHOR"] = "1";
        }

        using var process = Process.Start(start)!;
        process.StandardInput.Write(JsonSerializer.Serialize(new
        {
            tool_name = tool,
            tool_input = new Dictionary<string, string> { [field] = value },
        }));
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(30_000);

        return output.Contains("\"deny\"", StringComparison.Ordinal);
    }

    private static string? FindNode() =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator)
            .Select(directory => Path.Combine(directory, "node.exe"))
            .FirstOrDefault(File.Exists);

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
