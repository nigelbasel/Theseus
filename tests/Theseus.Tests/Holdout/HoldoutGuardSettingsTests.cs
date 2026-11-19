using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Theseus.Tests.Holdout;

/// <summary>
/// The holdout guard and the settings it trusts (verifier-card review S3 and S5): a holdout folder that is not an
/// absolute path, or is a whole drive, would refuse unrelated work, so it counts as unset; and no session may write
/// Theseus's own settings or board files with the file tools, since those decide the holdout and a card's role.
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
    public void AHoldoutFolderThatIsNotAFolderPath_IsTreatedAsUnset(string folder)
    {
        Assert.That(Guard("Read", "file_path", @"C:\work\repo\tools\holdout\holdout-guard.js", folder), Is.False);
        Assert.That(Guard("Bash", "command", "dotnet test", folder, cwd: @"C:\work\repo"), Is.False);
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
    public void TheseusOwnSettings_CanStillBeRead()
    {
        Assert.That(Guard("Read", "file_path", Path.Combine(_folder, "Theseus", "settings.json"), @"C:\holdout",
            localAppData: _folder), Is.False);
    }

    private bool Guard(string tool, string field, string value, string holdoutFolder, string cwd = @"C:\work\repo",
        string? localAppData = null)
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
        };
        start.Environment["THESEUS_SETTINGS"] = settingsFile;
        start.Environment.Remove("THESEUS_ROLE");
        start.Environment.Remove("THESEUS_HOLDOUT_AUTHOR");

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
