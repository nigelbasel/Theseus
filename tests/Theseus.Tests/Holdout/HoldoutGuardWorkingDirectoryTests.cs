using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Theseus.Tests.Holdout;

/// <summary>
/// The holdout guard against the routes verify part 1 found past it (verifier-card, fix pass 2): the session's working
/// directory (M1), Git Bash paths (S2), remotes written with .git or as SSH (S3), writes that leave the results folder
/// (S4), and the settings it reads (S1). A shell command run inside the holdout is refused for every role but author,
/// which is why a verifier starts in the code it checks (overnight decision 3, 2026-11-19).
/// </summary>
[TestFixture]
public class HoldoutGuardWorkingDirectoryTests
{
    private const string Folder = @"C:\holdout";
    private const string Code = @"C:\work\repo";

    private string _folder = string.Empty;

    [OneTimeSetUp]
    public void MakeFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), "theseus-holdout-guard-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TestCase("Read", "file_path", @"scenarios\stop.md")]
    [TestCase("Bash", "command", "ls")]
    [TestCase("Grep", "pattern", "stop")]
    public void NoRole_IsRefusedAnythingDoneFromInsideTheHoldout(string tool, string field, string value)
    {
        Assert.That(Guard(tool, field, value, cwd: Folder), Is.True);
    }

    [TestCase("rm scenarios/stop.md")]
    [TestCase("git commit -am x")]
    [TestCase("dotnet test")]
    public void AVerifier_RunsNoShellCommandInsideTheHoldout(string command)
    {
        Assert.That(Guard("Bash", "command", command, cwd: Folder, role: "verifier"), Is.True,
            "it reads the scenarios with the file tools; its builds run in the code it checks");
    }

    [Test]
    public void AVerifier_RunsShellCommandsInTheCode()
    {
        Assert.That(Guard("Bash", "command", "dotnet test Theseus.slnx", cwd: Code, role: "verifier"), Is.False);
    }

    [Test]
    public void AVerifier_SearchesTheHoldoutFromInsideIt()
    {
        Assert.That(Guard("Grep", "pattern", "stop", cwd: Folder, role: "verifier"), Is.False);
    }

    [TestCase(@"C:\holdout\results\..\scenarios\stop.md")]
    [TestCase(@"C:\holdout\results old\note.md")]
    [TestCase(@"C:\holdout\results")]
    public void AVerifier_CannotWriteOutsideResultsByAnotherRoute(string target)
    {
        Assert.That(Guard("Write", "file_path", target, cwd: Code, role: "verifier"), Is.True);
    }

    [TestCase("cat /c/holdout/scenarios/stop.md")]
    [TestCase("echo x >/c/holdout/scenarios/stop.md")]
    public void NoRole_IsRefusedAGitBashPathToTheHoldout(string command)
    {
        Assert.That(Guard("Bash", "command", command, cwd: Code), Is.True);
    }

    [TestCase("WebFetch", "url", "https://example.com/owner/holdout.git")]
    [TestCase("Bash", "command", "git clone git@example.com:owner/holdout.git")]
    public void NoRole_IsRefusedTheRemoteWrittenAnotherWay(string tool, string field, string value)
    {
        Assert.That(Guard(tool, field, value, cwd: Code), Is.True);
    }

    [Test]
    public void NoRole_IsNotRefusedARemoteThatOnlyEndsTheSame()
    {
        Assert.That(Guard("WebFetch", "url", "https://notexample.com/owner/holdout", cwd: Code), Is.False);
    }

    [Test]
    public void WithNoHoldoutSet_AVerifierIsRefusedNothing()
    {
        Assert.That(Guard("Write", "file_path", @"C:\work\repo\Program.cs", cwd: Code, role: "verifier", settings: "{}"),
            Is.False, "there is nothing to protect");
    }

    [Test]
    public void AFolderSetWithATrailingSeparator_StillProtectsTheHoldout()
    {
        Assert.That(Guard("Read", "file_path", @"C:\holdout\scenarios\stop.md", cwd: Code,
            settings: JsonSerializer.Serialize(new { holdoutFolder = @"C:\holdout\" })), Is.True);
    }

    [Test]
    public void ASettingsFileSavedWithAByteOrderMark_StillProtectsTheHoldout()
    {
        Assert.That(Guard("Read", "file_path", @"C:\holdout\scenarios\stop.md", cwd: Code,
            settings: "\uFEFF" + JsonSerializer.Serialize(new { holdoutFolder = Folder })), Is.True,
            "a guard that fails open on a valid settings file protects nothing");
    }

    private bool Guard(string tool, string field, string value, string cwd, string? role = null, string? settings = null)
    {
        var node = FindNode();
        if (node is null)
        {
            Assert.Ignore("Node is not installed here.");
        }

        var settingsFile = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(settingsFile,
            settings ?? JsonSerializer.Serialize(new { holdoutFolder = Folder, holdoutRemote = "example.com/owner/holdout" }),
            new UTF8Encoding(false));

        var script = Path.Combine(RepositoryRoot(), "tools", "holdout", "holdout-guard.js");
        var start = new ProcessStartInfo(node!, $"\"{script}\"")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        start.Environment["THESEUS_SETTINGS"] = settingsFile;
        start.Environment.Remove("THESEUS_ROLE");
        start.Environment.Remove("THESEUS_HOLDOUT_AUTHOR");

        if (role is not null)
        {
            start.Environment["THESEUS_ROLE"] = role;
        }

        using var process = Process.Start(start)!;
        process.StandardInput.Write(JsonSerializer.Serialize(new
        {
            cwd,
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
