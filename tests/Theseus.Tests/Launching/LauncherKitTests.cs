using Theseus.Core.Launching;

namespace Theseus.Tests.Launching;

[TestFixture]
public class LauncherKitTests
{
    private string _root = string.Empty;
    private string _installation = string.Empty;
    private string _kits = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-kit-{Guid.NewGuid():N}");
        _installation = Path.Combine(_root, "installation");
        _kits = Path.Combine(_root, "kits");

        Directory.CreateDirectory(_installation);
        Directory.CreateDirectory(_kits);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Prepare_RunsTheLauncherFromSomewhereTheInstallerWillNotBeReplacing()
    {
        var prepared = LauncherKit.Prepare(Installed(), _kits);

        Assert.That(prepared, Does.StartWith(_kits));
        Assert.That(File.Exists(prepared), Is.True);
    }

    [Test]
    public void Prepare_TakesTheFilesTheLauncherNeedsAndNotOnlyTheExecutable()
    {
        var prepared = LauncherKit.Prepare(Installed(), _kits);
        var directory = Path.GetDirectoryName(prepared)!;

        Assert.That(File.Exists(Path.Combine(directory, "Theseus.Core.dll")), Is.True);
        Assert.That(File.Exists(Path.Combine(directory, "Theseus.Launcher.runtimeconfig.json")), Is.True);
    }

    [Test]
    public void Prepare_TakesSubdirectoriesToo()
    {
        Directory.CreateDirectory(Path.Combine(_installation, "runtimes", "win-x64"));
        File.WriteAllText(Path.Combine(_installation, "runtimes", "win-x64", "native.dll"), "native");

        var directory = Path.GetDirectoryName(LauncherKit.Prepare(Installed(), _kits))!;

        Assert.That(File.Exists(Path.Combine(directory, "runtimes", "win-x64", "native.dll")), Is.True);
    }

    [Test]
    public void Prepare_CopiesOncePerBuildRatherThanOncePerSession()
    {
        var first = LauncherKit.Prepare(Installed(), _kits);
        var marker = Path.Combine(Path.GetDirectoryName(first)!, "witness.txt");

        File.WriteAllText(marker, "still here");

        Assert.That(LauncherKit.Prepare(Installed(), _kits), Is.EqualTo(first));
        Assert.That(File.Exists(marker), Is.True, "the kit was copied again rather than reused");
    }

    [Test]
    public void Prepare_ReplacesTheKitWhenTheInstallationHasBeenUpgraded()
    {
        var first = LauncherKit.Prepare(Installed(), _kits);

        Directory.Delete(_installation, recursive: true);
        Directory.CreateDirectory(_installation);

        var second = LauncherKit.Prepare(Installed(version: "2026.08.27.1"), _kits);

        Assert.That(second, Is.Not.EqualTo(first));
        Assert.That(File.Exists(second), Is.True);
    }

    [Test]
    public void Prepare_ClearsAwayTheKitsOfBuildsNoLongerInUse()
    {
        LauncherKit.Prepare(Installed(), _kits);

        Directory.Delete(_installation, recursive: true);
        Directory.CreateDirectory(_installation);

        LauncherKit.Prepare(Installed(version: "2026.08.27.1"), _kits);

        Assert.That(Directory.GetDirectories(_kits), Has.Length.EqualTo(1));
    }

    [Test]
    public void Prepare_CopiesAgainWhenAnEarlierAttemptWasCutShort()
    {
        var prepared = LauncherKit.Prepare(Installed(), _kits);
        var directory = Path.GetDirectoryName(prepared)!;

        File.Delete(Path.Combine(directory, ".complete"));
        File.WriteAllText(Path.Combine(directory, "Theseus.Core.dll"), "truncated");

        LauncherKit.Prepare(Installed(), _kits);

        Assert.That(File.ReadAllText(Path.Combine(directory, "Theseus.Core.dll")), Is.EqualTo("core"));
    }

    [Test]
    public void Prepare_FallsBackToTheInstalledLauncherRatherThanLeavingStartDead()
    {
        var missing = Path.Combine(_installation, "Theseus.Launcher.exe");

        Assert.That(LauncherKit.Prepare(missing, _kits), Is.EqualTo(missing));
    }

    private string Installed(string version = "2026.08.26.1")
    {
        var launcher = Path.Combine(_installation, "Theseus.Launcher.exe");

        if (!File.Exists(launcher) || File.ReadAllText(launcher) != version)
        {
            File.WriteAllText(launcher, version);
        }

        File.WriteAllText(Path.Combine(_installation, "Theseus.Core.dll"), "core");
        File.WriteAllText(Path.Combine(_installation, "Theseus.Launcher.runtimeconfig.json"), "{}");

        return launcher;
    }

    [Test]
    public void Prepare_LeavesTheKitOfABuildASessionIsStillRunningFrom()
    {
        var first = LauncherKit.Prepare(Installed(), _kits);
        var directory = Path.GetDirectoryName(first)!;

        Upgrade();

        LauncherKit.Prepare(Installed(version: "2026.08.27.1"), _kits, () => [first]);

        Assert.That(File.Exists(Path.Combine(directory, "Theseus.Core.dll")), Is.True,
            "a recursive delete guts a kit in use rather than skipping it, and the session then dies");
        Assert.That(Directory.GetFiles(directory), Has.Length.GreaterThan(1));
    }

    [Test]
    public void Prepare_ClearsAwayAKitOnceNothingIsRunningFromIt()
    {
        var first = LauncherKit.Prepare(Installed(), _kits);

        Upgrade();

        LauncherKit.Prepare(Installed(version: "2026.08.27.1"), _kits, () => []);

        Assert.That(Directory.Exists(Path.GetDirectoryName(first)!), Is.False);
    }

    [Test]
    public void Prepare_LeavesAKitWholeWhenAFileInItCannotEvenBeMoved()
    {
        var first = LauncherKit.Prepare(Installed(), _kits);
        var directory = Path.GetDirectoryName(first)!;
        var held = Path.Combine(directory, "Theseus.Core.dll");

        using var _ = new FileStream(held, FileMode.Open, FileAccess.Read, FileShare.Read);

        Upgrade();

        LauncherKit.Prepare(Installed(version: "2026.08.27.1"), _kits, () => []);

        Assert.That(File.Exists(held), Is.True,
            "renaming a directory holding an open file fails, which is the point: nothing is deleted");
    }

    private void Upgrade()
    {
        Directory.Delete(_installation, recursive: true);
        Directory.CreateDirectory(_installation);
    }
}
