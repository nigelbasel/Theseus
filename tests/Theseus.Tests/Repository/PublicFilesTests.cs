using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Theseus.Tests.Repository;

/// <summary>
/// The repo is public, and the method kit under method/ is written by
/// sessions working on the owner's machine. They carry no machine paths and no card ids: placeholders such as
/// &lt;scratch&gt;, &lt;worktree&gt; and "the workstream log" instead (method rule P19, Nigel, 2026-11-19). Every
/// stage 0 record broke this before the rule existed.
/// </summary>
[TestFixture]
public class PublicFilesTests
{
    private static readonly string[] Folders = ["method"];

    private static readonly Regex MachineSpecific = new(
        @"\\Users\\(?![<%])\w"
        + @"|AppData\\Local\\Temp\\claude"
        + @"|\b[A-Za-z]:\\(?!Program Files\\|fake\\)[^\s`'""|)<>]+\\"
        + @"|\bgit-\d+\b",
        RegexOptions.IgnoreCase);

    [TestCase(@"logged in `C:\Users\someone\.claude\logs\a-card.log`", Description = "a user profile")]
    [TestCase(@"copied to C:\Users\someone\AppData\Local\Temp\claude\0a1b\scratchpad\copy", Description = "a session's scratch folder")]
    [TestCase(@"the worktree at D:\work\feature\Theseus", Description = "a drive path with folders")]
    [TestCase(@"run by card git-7 on main", Description = "a card id")]
    public void AMachinePathOrCardId_IsFound(string line)
    {
        Assert.That(MachineSpecific.IsMatch(line), Is.True);
    }

    [TestCase(@"copied to `<scratch>\close-step-verify\copy`", Description = "a placeholder")]
    [TestCase(@"logged at the stage start in the workstream log", Description = "a description")]
    [TestCase(@"`& ""C:\Program Files\Git\bin\bash.exe""`", Description = "a standard install path")]
    [TestCase(@"`& ""C:\fake\Git\bin\bash.exe"" ""C:\fake\lock.sh""`", Description = "a test input naming a fake path")]
    [TestCase(@"the drive root `E:\` took 1.9 ms", Description = "a bare drive root")]
    [TestCase(@"%LOCALAPPDATA%\Theseus\logs\theseus-<yyyyMMdd>.log", Description = "an environment variable")]
    public void APlaceholderOrGenericPath_IsAllowed(string line)
    {
        Assert.That(MachineSpecific.IsMatch(line), Is.False);
    }

    [Test]
    public void TheMethodsRecords_CarryNoMachinePathsOrCardIds()
    {
        var root = RepositoryRoot();

        var found = Folders
            .Select(folder => Path.Combine(root, folder))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*.md", SearchOption.AllDirectories))
            .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, number: index + 1)))
            .Where(entry => MachineSpecific.IsMatch(entry.line))
            .Select(entry => $"{Path.GetRelativePath(root, entry.file)}:{entry.number}")
            .ToList();

        Assert.That(found, Is.Empty, "use a placeholder such as <scratch> or <worktree>, or a description");
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
