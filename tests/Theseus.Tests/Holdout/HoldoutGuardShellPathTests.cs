using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Theseus.Tests.Holdout;

/// <summary>
/// The holdout guard against the routes the verifier card left open (verify part 1 attempt 2, C11 to C13, C6; its
/// re-verify, C1): a shell command or a search that reaches a holdout by a path relative to where it runs, a path
/// written the Git Bash or WSL way with an escaped space, a remote typed in another form than the setting, a folder
/// setting with no drive, and a shell command that rewrites Theseus's own files by a relative or variable path.
/// </summary>
[TestFixture]
public class HoldoutGuardShellPathTests
{
    private const string Folder = @"C:\holdout";
    private const string Code = @"C:\work\repo";
    private const string Listed = @"C:\listed";

    private string _local = string.Empty;

    [OneTimeSetUp]
    public void MakeDataFolder()
    {
        _local = Path.Combine(Path.GetTempPath(), "theseus-holdout-shell-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_local, "Theseus"));
        File.WriteAllText(Path.Combine(_local, "Theseus", "holdout-folders.json"),
            JsonSerializer.Serialize(new { folders = new[] { new { project = "listed", folder = Listed } } }),
            new UTF8Encoding(false));
    }

    [TestCase("Bash", "cd ../../holdout && cat scenarios/stop.md")]
    [TestCase("Bash", "cat ./../../holdout/scenarios/stop.md")]
    [TestCase("Bash", "cat \"../../holdout/scenarios/stop.md\"")]
    [TestCase("Bash", "cat ../../listed/scenarios/stop.md")]
    [TestCase("PowerShell", @"Get-Content ..\..\holdout\scenarios\stop.md")]
    public void NoRole_IsRefusedAHoldoutReachedByARelativePath(string tool, string command)
    {
        Assert.That(Guard(tool, "command", command, cwd: Code), Is.True, "attempt 2, C11");
    }

    [TestCase("Bash", "cd ../../holdout && rm scenarios/stop.md")]
    [TestCase("Bash", "git -C ../../holdout commit -am x")]
    [TestCase("Bash", "cp report.md ../../holdout/results/")]
    [TestCase("PowerShell", @"Set-Location ..\..\holdout; Remove-Item scenarios\stop.md")]
    public void AVerifier_RunsNoShellCommandOnTheHoldoutByARelativePath(string tool, string command)
    {
        Assert.That(Guard(tool, "command", command, cwd: Code, role: "verifier"), Is.True, "attempt 2, C11");
    }

    [Test]
    public void NoRole_IsRefusedAGlobReachingTheHoldoutRelatively()
    {
        Assert.That(Guard("Glob", "pattern", "../../holdout/**/*.md", cwd: Code), Is.True, "attempt 2, n14");
    }

    [TestCase("cat ../other/notes.md")]
    [TestCase("cat ../../holdout-old/notes.md")]
    [TestCase("git commit -m \"the holdout guard\"")]
    [TestCase("dotnet test ../repo/Theseus.slnx")]
    public void AVerifier_StillRunsCommandsThatReachNoHoldout(string command)
    {
        Assert.That(Guard("Bash", "command", command, cwd: Code, role: "verifier"), Is.False);
    }

    [Test]
    public void NoRole_IsRefusedAGitBashPathWithAnEscapedSpace()
    {
        Assert.That(Guard("Bash", "command", @"cat /c/My\ Holdout/scenarios/stop.md", cwd: Code,
            settings: JsonSerializer.Serialize(new { holdoutFolder = @"C:\My Holdout" })), Is.True, "attempt 2, C12");
    }

    [Test]
    public void NoRole_IsRefusedAWslPathToTheHoldout()
    {
        Assert.That(Guard("Bash", "command", "cat /mnt/c/holdout/scenarios/stop.md", cwd: Code), Is.True, "attempt 2, C12");
    }

    [TestCase("Bash", "command", "gh repo clone example.com/owner/holdout")]
    [TestCase("Bash", "command", "git clone git@example.com:owner/holdout")]
    [TestCase("WebFetch", "url", "https://example.com/owner/holdout")]
    public void ARemoteTypedAsAUrl_StillGuardsEveryFormOfIt(string tool, string field, string value)
    {
        Assert.That(Guard(tool, field, value, cwd: Code,
            settings: JsonSerializer.Serialize(new { holdoutFolder = Folder, holdoutRemote = "https://example.com/owner/holdout.git" })),
            Is.True, "attempt 2, C13: the hint asks for host/owner/repo, but a URL is what gets pasted");
    }

    [TestCase("gh api repos/owner/holdout/contents/scenarios")]
    [TestCase("gh repo clone owner/holdout")]
    public void NoRole_IsRefusedTheRemoteByOwnerAndNameAlone(string command)
    {
        Assert.That(Guard("Bash", "command", command, cwd: Code), Is.True, "attempt 2, n28");
    }

    [TestCase("gh repo clone owner/holdout-old")]
    [TestCase("gh repo clone someone/holdout")]
    public void NoRole_IsNotRefusedAnotherRepo(string command)
    {
        Assert.That(Guard("Bash", "command", command, cwd: Code), Is.False);
    }

    [Test]
    public void AFolderTypedTheGitBashWay_GuardsThatFolder()
    {
        Assert.That(Guard("Read", "file_path", @"C:\holdout\scenarios\stop.md", cwd: Code,
            settings: JsonSerializer.Serialize(new { holdoutFolder = "/c/holdout" })), Is.True,
            "re-verify C1: it guarded a folder on the guard's own drive instead");
    }

    [Test]
    public void AFolderWithNoDrive_IsTreatedAsUnset()
    {
        var onTheGuardsDrive = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "holdout", "stop.md");

        Assert.That(Guard("Read", "file_path", onTheGuardsDrive, cwd: Code,
            settings: JsonSerializer.Serialize(new { holdoutFolder = @"\holdout" })), Is.False,
            "re-verify C1: it guarded whichever drive the guard happened to run on");
    }

    [Test]
    public void TheseusOwnSettings_CannotBeRewrittenByARelativeShellPath()
    {
        Assert.That(Guard("Bash", "command", "echo {} > settings.json", cwd: Path.Combine(_local, "Theseus")), Is.True,
            "attempt 2, C6");
    }

    [TestCase("Bash", "echo {} > \"$LOCALAPPDATA/Theseus/board.json\"")]
    [TestCase("Bash", @"echo {} > %LOCALAPPDATA%\Theseus\settings.json")]
    [TestCase("PowerShell", @"Set-Content $env:LOCALAPPDATA\Theseus\board.json '{}'")]
    public void TheseusOwnFiles_CannotBeRewrittenThroughTheDataFoldersVariable(string tool, string command)
    {
        Assert.That(Guard(tool, "command", command, cwd: Code), Is.True, "attempt 2, C6");
    }

    [Test]
    public void ASettingsFileInTheCode_IsNotTheseus()
    {
        Assert.That(Guard("Bash", "command", "cat settings.json", cwd: Code), Is.False);
    }

    private bool Guard(string tool, string field, string value, string cwd, string? role = null, string? settings = null)
    {
        var node = FindNode();
        if (node is null)
        {
            Assert.Ignore("Node is not installed here.");
        }

        var settingsFile = Path.Combine(_local, Guid.NewGuid().ToString("N") + ".json");
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
        start.Environment["LOCALAPPDATA"] = _local;
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
