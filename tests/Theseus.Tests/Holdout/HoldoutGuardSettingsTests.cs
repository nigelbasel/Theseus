using Theseus.Core.Links;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Theseus.Tests.Holdout;

/// <summary>
/// The holdout guard and the settings it trusts (verifier-card review S3 and S5): a holdout folder that is not an
/// absolute path, or is a whole drive, would refuse unrelated work, so it counts as unset; and while a holdout is set no
/// session may write Theseus's own settings or board files with the file tools, since those decide the holdout and a
/// card's role. With no folder chosen the default holdouts folder is guarded just as a chosen one is (1.1e AC 6c).
/// </summary>
[TestFixture]
public class HoldoutGuardSettingsTests
{
    private string _folder = string.Empty;

    [OneTimeSetUp]
    public void MakeFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), "theseus-holdout-guard-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TestCase("holdout")]
    [TestCase(@"C:\")]
    [TestCase("C:")]
    public void AHoldoutFolderThatIsNotAFolderPath_IsTreatedAsUnset_SoTheDefaultIsGuarded(string folder)
    {
        Assert.That(Guard("Read", "file_path", @"C:\work\repo\tools\holdout\holdout-guard.js", folder, localAppData: _folder), Is.False);
        Assert.That(Guard("Bash", "command", "dotnet test", folder, cwd: @"C:\work\repo", localAppData: _folder), Is.False);
        Assert.That(Guard("Read", "file_path", Path.Combine(_folder, "Theseus", "holdouts", "p", "a.md"), folder, localAppData: _folder),
            Is.True);
    }

    [TestCase("settings.json")]
    [TestCase("board.json")]
    public void TheseusOwnSettingsAndBoard_CannotBeWrittenByASession(string file)
    {
        var target = Path.Combine(_folder, "Theseus", file);

        Assert.That(Guard("Write", "file_path", target, @"C:\holdout", localAppData: _folder), Is.True);
        Assert.That(Guard("Edit", "file_path", target, @"C:\holdout", localAppData: _folder), Is.True);
    }

    [Test]
    public void ARelativeHoldoutFolder_IsNotReadAsAFolderBesideTheGuard()
    {
        Assert.That(Guard("Read", "file_path", Path.Combine(_folder, "holdout", "stop.md"), "holdout",
            workingDirectory: _folder), Is.False, "a relative setting counts as unset, wherever the guard happens to run");
    }

    [Test]
    public void TheseusOwnBoard_CannotBeWrittenAsANotebook()
    {
        Assert.That(Guard("NotebookEdit", "notebook_path", Path.Combine(_folder, "Theseus", "board.json"), @"C:\holdout",
            localAppData: _folder), Is.True);
    }

    [Test]
    public void TheseusOwnBoard_CannotBeWrittenByARelativePath()
    {
        Assert.That(Guard("Write", "file_path", "board.json", @"C:\holdout", cwd: Path.Combine(_folder, "Theseus"),
            localAppData: _folder), Is.True);
    }

    [Test]
    public void WithNoFolderChosen_TheDefaultHoldoutIsGuarded_AsAChosenOneIs()
    {
        var scenario = Path.Combine(_folder, "Theseus", "holdouts", "theseus", "stop.md");

        Assert.That(Guard("Read", "file_path", scenario, string.Empty, localAppData: _folder), Is.True, "a building session may not read it");
        Assert.That(Guard("Grep", "path", Path.Combine(_folder, "Theseus", "holdouts"), string.Empty, localAppData: _folder), Is.True);
        Assert.That(Guard("Bash", "command", $"type \"{scenario}\"", string.Empty, localAppData: _folder), Is.True);
        Assert.That(Guard("Write", "file_path", Path.Combine(_folder, "Theseus", "board.json"), string.Empty, localAppData: _folder),
            Is.True, "and Theseus's own files are protected, since a holdout is always in force");
        Assert.That(Guard("Read", "file_path", Path.Combine(_folder, "Theseus", "holdouts-notes.md"), string.Empty, localAppData: _folder),
            Is.False, "a name only counts whole");
    }

    // A holdout-folders.json as Theseus writes it, with one folder found beside a fixture repo (1.1e AC 6e).
    private string ListBesideRepo()
    {
        var beside = Path.Combine(_folder, "src", "Theseus-holdout");
        var theseus = Path.Combine(_folder, "Theseus");
        Directory.CreateDirectory(theseus);
        HoldoutFolderFile.Write(Path.Combine(theseus, HoldoutFolderFile.FileName),
            [new HoldoutFolderEntry("theseus", beside, HoldoutSource.BesideRepo)]);

        return beside;
    }

    [Test]
    public void AFolderListedByTheseus_IsGuarded_AsTheDefaultIs()
    {
        var beside = ListBesideRepo();

        Assert.That(Guard("Read", "file_path", Path.Combine(beside, "scenario.md"), string.Empty, localAppData: _folder), Is.True);
        Assert.That(Guard("Grep", "path", beside, string.Empty, localAppData: _folder), Is.True);
        Assert.That(Guard("Bash", "command", "dir", string.Empty, cwd: beside, localAppData: _folder), Is.True, "no shell inside it");
        Assert.That(Guard("Read", "file_path", Path.Combine(_folder, "src", "Theseus", "code.cs"), string.Empty, localAppData: _folder),
            Is.False, "the repo beside it is not the holdout");
    }

    [Test]
    public void AVerifierWithAListedFolder_WritesOnlyInItsResults()
    {
        var beside = ListBesideRepo();

        Assert.That(Guard("Write", "file_path", Path.Combine(beside, "results", "verify.md"), string.Empty, localAppData: _folder,
            role: "verifier"), Is.False);
        Assert.That(Guard("Write", "file_path", Path.Combine(beside, "scenario.md"), string.Empty, localAppData: _folder,
            role: "verifier"), Is.True);
    }

    [Test]
    public void AFolderNamedForTheConvention_IsGuarded_EvenNamedRelatively_OrNotYetListed()
    {
        Assert.That(Guard("Bash", "command", "cat ../Repo-holdout/a.md", string.Empty, localAppData: _folder), Is.True);
        Assert.That(Guard("PowerShell", "command", @"Get-Content ..\Repo-holdout\a.md", string.Empty, localAppData: _folder), Is.True);
        Assert.That(Guard("Glob", "pattern", "../Repo-holdout/**", string.Empty, localAppData: _folder), Is.True);
        Assert.That(Guard("Read", "file_path", Path.Combine(_folder, "src", "Repo-holdout", "a.md"), string.Empty, localAppData: _folder), Is.True);
        Assert.That(Guard("Read", "file_path", Path.Combine(_folder, "src", "Repo", "holdout-notes.md"), string.Empty, localAppData: _folder),
            Is.False, "only a folder named <repo>-holdout");
        Assert.That(Guard("Write", "file_path", Path.Combine(_folder, "src", "Repo-holdout", "results", "r.md"), string.Empty,
            localAppData: _folder, role: "verifier"), Is.False, "a verifier writes in its results");
    }

    [Test]
    public void TheHoldoutFoldersFile_CannotBeRemovedByAShellCommand_AndOneBadEntryDropsNothing()
    {
        var beside = ListBesideRepo();
        File.WriteAllText(Path.Combine(_folder, "Theseus", HoldoutFolderFile.FileName),
            $$"""{"folders":[{"folder":{{System.Text.Json.JsonSerializer.Serialize(Path.Combine(_folder, "elsewhere"))}}},null]}""");

        Assert.That(Guard("Bash", "command", "rm \"$LOCALAPPDATA/Theseus/holdout-folders.json\"", string.Empty, localAppData: _folder), Is.True);
        Assert.That(Guard("Read", "file_path", Path.Combine(_folder, "elsewhere", "a.md"), string.Empty, localAppData: _folder), Is.True,
            "the good entry still counts");
        Assert.That(beside, Does.EndWith("-holdout"));
    }

    [Test]
    public void TheHoldoutFoldersFile_CannotBeWrittenByASession()
    {
        ListBesideRepo();

        Assert.That(Guard("Write", "file_path", Path.Combine(_folder, "Theseus", HoldoutFolderFile.FileName), string.Empty,
            localAppData: _folder), Is.True);
        Assert.That(Guard("Read", "file_path", Path.Combine(_folder, "Theseus", HoldoutFolderFile.FileName), string.Empty,
            localAppData: _folder), Is.False, "reading it is fine");
    }

    [Test]
    public void AVerifierWithTheDefaultHoldout_WritesItsReportInItsProjectsResults_AndNowhereElse()
    {
        var project = Path.Combine(_folder, "Theseus", "holdouts", "theseus");

        Assert.That(Guard("Write", "file_path", Path.Combine(project, "results", "2026-11-21-verify.md"), string.Empty,
            localAppData: _folder, role: "verifier"), Is.False, "review of 1.1e PR 2b, finding 1");
        Assert.That(Guard("Write", "file_path", Path.Combine(project, "results", "handoff", "HANDOFF-v.md"), string.Empty,
            localAppData: _folder, role: "verifier"), Is.False);
        Assert.That(Guard("Write", "file_path", Path.Combine(project, "scenario.md"), string.Empty,
            localAppData: _folder, role: "verifier"), Is.True, "a scenario");
        Assert.That(Guard("Write", "file_path", Path.Combine(project, "results"), string.Empty,
            localAppData: _folder, role: "verifier"), Is.True, "the results folder itself");
        Assert.That(Guard("Write", "file_path", Path.Combine(_folder, "Theseus", "holdouts", "results", "x.md"), string.Empty,
            localAppData: _folder, role: "verifier"), Is.True, "a project named results has no results folder of its own there");
        Assert.That(Guard("Read", "file_path", Path.Combine(project, "scenario.md"), string.Empty,
            localAppData: _folder, role: "verifier"), Is.False, "it reads the scenarios");
    }

    [Test]
    public void AScenarioWriter_MayWriteInTheDefaultHoldout()
    {
        Assert.That(Guard("Write", "file_path", Path.Combine(_folder, "Theseus", "holdouts", "theseus", "stop.md"), string.Empty,
            localAppData: _folder, author: true), Is.False);
    }

    [Test]
    public void TheseusOwnSettings_CanStillBeRead()
    {
        Assert.That(Guard("Read", "file_path", Path.Combine(_folder, "Theseus", "settings.json"), @"C:\holdout",
            localAppData: _folder), Is.False);
    }

    private bool Guard(string tool, string field, string value, string holdoutFolder, string cwd = @"C:\work\repo",
        string? localAppData = null, string? workingDirectory = null, bool author = false, string? role = null)
    {
        var node = FindNode();
        if (node is null)
        {
            Assert.Ignore("Node is not installed here.");
        }

        var settingsFile = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(settingsFile, JsonSerializer.Serialize(new { holdoutFolder }), new UTF8Encoding(false));

        var script = Path.Combine(RepositoryRoot(), "tools", "holdout", "holdout-guard.js");
        var start = new ProcessStartInfo(node!, $"\"{script}\"")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? string.Empty,
        };
        start.Environment["THESEUS_SETTINGS"] = settingsFile;
        start.Environment.Remove("THESEUS_ROLE");
        start.Environment.Remove("THESEUS_HOLDOUT_AUTHOR");

        if (author)
        {
            start.Environment["THESEUS_HOLDOUT_AUTHOR"] = "1";
        }

        if (role is not null)
        {
            start.Environment["THESEUS_ROLE"] = role;
        }

        if (localAppData is not null)
        {
            start.Environment["LOCALAPPDATA"] = localAppData;
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
