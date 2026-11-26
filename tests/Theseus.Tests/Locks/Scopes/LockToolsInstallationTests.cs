using Theseus.Core.Locks;

namespace Theseus.Tests.Locks.Scopes;

[TestFixture]
public class LockToolsInstallationTests
{
    private string _config = string.Empty;

    [SetUp]
    public void SetUp() => _config = Path.Combine(Path.GetTempPath(), $"theseus-lock-tools-{Guid.NewGuid():N}", ".claude");

    [TearDown]
    public void TearDown()
    {
        string root = Path.GetDirectoryName(_config)!;

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void NothingInstalled_SaysSo()
    {
        LockToolsState state = new LockToolsInstallation(_config).State();

        Assert.That(state.Line, Is.EqualTo("Not installed"));
        Assert.That(state.Files.Select(file => file.Name), Is.EqualTo(LockToolsBundle.FileNames));
    }

    [Test]
    public void Install_WritesEveryFile_AndThenTheyAreUpToDate()
    {
        LockToolsInstallation installation = new(_config);

        LockToolsOutcome outcome = installation.Install();

        Assert.That(outcome.Succeeded, Is.True, outcome.Message);
        Assert.That(installation.State().Line, Is.EqualTo("Up to date"));
        Assert.That(File.ReadAllText(Path.Combine(_config, "bin", "command-classes.json")), Is.EqualTo(LockToolsBundle.Text("command-classes.json")));
    }

    [Test]
    public void AnOlderCopy_IsKept_AndUndoPutsItBack()
    {
        string script = Path.Combine(_config, "bin", "ndc-test-lock.sh");
        Directory.CreateDirectory(Path.GetDirectoryName(script)!);
        File.WriteAllText(script, "# an older client\n");
        LockToolsInstallation installation = new(_config);

        Assert.That(installation.State().Line, Does.StartWith("Out of date"));
        installation.Install();
        Assert.That(installation.State().CanUndo, Is.True);

        LockToolsOutcome undone = installation.Undo();

        Assert.That(undone.Succeeded, Is.True, undone.Message);
        Assert.That(File.ReadAllText(script), Is.EqualTo("# an older client\n"));
    }

    [Test]
    public void ACopyDifferingOnlyInLineEndings_IsUpToDate()
    {
        string guard = Path.Combine(_config, "bin", "ndc-test-lock-guard.js");
        Directory.CreateDirectory(Path.GetDirectoryName(guard)!);
        File.WriteAllText(guard, LockToolsBundle.Text("ndc-test-lock-guard.js").Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal));

        Assert.That(new LockToolsInstallation(_config).State().Files.Single(file => file.Name == "ndc-test-lock-guard.js").IsUpToDate, Is.True);
    }
}
