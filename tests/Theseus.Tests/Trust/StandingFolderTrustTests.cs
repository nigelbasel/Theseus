using System.Text.Json.Nodes;
using Theseus.Core.Settings;
using Theseus.Core.Trust;
using Theseus.Tests.ViewModels;

namespace Theseus.Tests.Trust;

/// <summary>
/// Folder trust the owner gives once and Theseus keeps (objective 1.3, Nigel 2026-11-26: "it will be impossible to keep
/// track of all of the folders"): with the standing consent on, a card's folders are trusted by their key before launch,
/// only inside the projects and clones folders, never a home folder, a drive's root or a holdout.
/// </summary>
[TestFixture]
public class StandingFolderTrustTests
{
    private string _root = null!;
    private string _config = null!;
    private string _undo = null!;
    private string _projects = null!;
    private string _clones = null!;
    private string _holdout = null!;
    private MemorySettingsStore _settings = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "theseus-standing-" + Guid.NewGuid().ToString("N"));
        _config = Path.Combine(Directory.CreateDirectory(_root).FullName, "config.json");
        _undo = Path.Combine(_root, "undo.json");
        _projects = Directory.CreateDirectory(Path.Combine(_root, "hq", "projects")).FullName;
        _clones = Directory.CreateDirectory(Path.Combine(_root, "src")).FullName;
        _holdout = Repository("src", "host", "owner", "repo-holdout");
        File.WriteAllText(_config, new JsonObject().ToJsonString());
        _settings = new MemorySettingsStore
        {
            Settings = TheseusSettings.Default with { ProjectsFolder = _projects, ClonesFolder = _clones },
        };
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Keep_WithTheConsentOff_WritesNothing()
    {
        var main = Repository("src", "host", "owner", "repo");

        var outcome = Standing().Keep([main]);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Trusted, Is.Empty);
            Assert.That(File.ReadAllText(_config), Is.EqualTo(new JsonObject().ToJsonString()), "the file is not touched");
            Assert.That(File.Exists(_undo), Is.False);
        });
    }

    [Test]
    public void Keep_TrustsARepositoryInTheClonesFolder_ByItsRoot()
    {
        var main = Repository("src", "host", "owner", "repo");
        var inside = Directory.CreateDirectory(Path.Combine(main, "src", "lib")).FullName;
        SwitchOn();

        var outcome = Standing().Keep([inside]);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Trusted, Is.EqualTo(new[] { Spelled(main) }));
            Assert.That(IsTrusted(Spelled(main)), Is.True);
        });
    }

    [Test]
    public void Keep_TrustsAWorktreeOutsideBothFolders_ByItsMainClone()
    {
        var main = Repository("src", "host", "owner", "repo");
        var worktree = Worktree(main, "work", "feature", "repo");
        SwitchOn();

        var outcome = Standing().Keep([worktree]);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Trusted, Is.EqualTo(new[] { Spelled(main) }), "a worktree is trusted through its main clone");
            Assert.That(IsTrusted(Spelled(worktree)), Is.False, "no record is made for the worktree itself");
            Assert.That(outcome.LeftForAClick, Is.Empty);
        });
    }

    [Test]
    public void Keep_TrustsANestedRepository_ByItsOwnRoot()
    {
        var outer = Repository("src", "host", "owner", "outer");
        var nested = Repository("src", "host", "owner", "outer", "modules", "inner");
        SwitchOn();

        var outcome = Standing().Keep([nested]);

        Assert.That(outcome.Trusted, Is.EqualTo(new[] { Spelled(nested) }), $"not {Spelled(outer)}: a nested repository is its own");
    }

    [Test]
    public void Keep_TrustsAPlainFolderInTheProjectsFolder_ByItself()
    {
        var home = Directory.CreateDirectory(Path.Combine(_projects, "some-project")).FullName;
        SwitchOn();

        var outcome = Standing().Keep([home]);

        Assert.That(outcome.Trusted, Is.EqualTo(new[] { Spelled(home) }));
    }

    [Test]
    public void Keep_TrustsTheProjectsFolder_ByTheRepositoryItIsIn()
    {
        var hq = Path.Combine(_root, "hq");
        Directory.CreateDirectory(Path.Combine(hq, ".git"));
        var home = Directory.CreateDirectory(Path.Combine(_projects, "some-project")).FullName;
        SwitchOn();

        var outcome = Standing().Keep([home]);

        Assert.That(outcome.Trusted, Is.EqualTo(new[] { Spelled(hq) }), "the projects folder's own repository is what Claude Code keys it by");
    }

    [Test]
    public void Keep_LeavesAFolderOutsideBothFolders_ForAClick()
    {
        var elsewhere = Repository("elsewhere", "repo");
        SwitchOn();

        var outcome = Standing().Keep([elsewhere]);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Trusted, Is.Empty);
            Assert.That(outcome.LeftForAClick.Select(need => need.Key), Is.EqualTo(new[] { Spelled(elsewhere) }));
            Assert.That(IsTrusted(Spelled(elsewhere)), Is.False);
        });
    }

    [Test]
    public void Keep_NeverTrustsAHoldout_OrAFolderInOne()
    {
        var inside = Directory.CreateDirectory(Path.Combine(_holdout, "scenarios")).FullName;
        SwitchOn();

        var outcome = Standing().Keep([_holdout, inside]);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Trusted, Is.Empty);
            Assert.That(IsTrusted(Spelled(_holdout)), Is.False);
        });
    }

    [Test]
    public void Keep_NeverTrustsAFolderNamedAsAHoldout_EvenBeforeTheHoldoutsAreRead()
    {
        var beside = Repository("src", "host", "owner", "other" + Theseus.Core.Links.HomeRepo.HoldoutSuffix);
        SwitchOn();

        var outcome = new StandingFolderTrust(new ClaudeFolderTrust(_config, _undo), _settings, () => [], Path.Combine(_root, "user")).Keep([beside]);

        Assert.That(outcome.Trusted, Is.Empty);
    }

    [Test]
    public void Keep_NeverTrustsAFolderHoldingAHoldout()
    {
        var holding = Directory.CreateDirectory(Path.Combine(_projects, "holding")).FullName;
        var holdout = Directory.CreateDirectory(Path.Combine(holding, "checks")).FullName;
        SwitchOn();

        var outcome = new StandingFolderTrust(new ClaudeFolderTrust(_config, _undo), _settings, () => [holdout], _root).Keep([holding]);

        Assert.That(outcome.Trusted, Is.Empty, "trusting a plain folder covers its subfolders, the holdout among them");
    }

    [Test]
    public void Keep_NeverTrustsTheHomeFolder()
    {
        var home = Directory.CreateDirectory(Path.Combine(_projects, "me")).FullName;
        SwitchOn();

        var outcome = new StandingFolderTrust(new ClaudeFolderTrust(_config, _undo), _settings, () => [_holdout], home).Keep([home]);

        Assert.That(outcome.Trusted, Is.Empty);
    }

    [Test]
    public void Keep_NeverTrustsADrivesRoot()
    {
        var drive = Path.GetPathRoot(_root)!;
        _settings.Settings = _settings.Settings with { ClonesFolder = drive };
        SwitchOn();

        var outcome = Standing().Keep([drive]);

        Assert.That(outcome.Trusted, Is.Empty);
    }

    [Test]
    public void Keep_NeverTrustsTheClonesFolderItself()
    {
        SwitchOn();

        var outcome = Standing().Keep([_clones]);

        Assert.That(outcome.Trusted, Is.Empty, "a plain clones folder trusted whole would cover every non-repository folder in it");
    }

    [Test]
    public void Keep_WritesOnlyWhatIsMissing()
    {
        var main = Repository("src", "host", "owner", "repo");
        SwitchOn();
        Standing().Keep([main]);

        var again = Standing().Keep([main, Worktree(main, "work", "feature", "repo")]);

        Assert.That(again.Trusted, Is.Empty);
    }

    [Test]
    public void Undo_TakesBackWhatItSet_AndSwitchesTheConsentOff()
    {
        var main = Repository("src", "host", "owner", "repo");
        var projects = (JsonObject)JsonNode.Parse(File.ReadAllText(_config))!;
        projects["projects"] = new JsonObject { ["E:/owners-own"] = new JsonObject { ["hasTrustDialogAccepted"] = true } };
        File.WriteAllText(_config, projects.ToJsonString());
        SwitchOn();
        var standing = Standing();
        standing.Keep([main]);

        var outcome = standing.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True, outcome.Message);
            Assert.That(IsTrusted(Spelled(main)), Is.False);
            Assert.That(IsTrusted("E:/owners-own"), Is.True, "a record the owner gave himself is not Theseus's to take back");
            Assert.That(standing.IsOn, Is.False, "otherwise the next start would trust it all again");
        });
    }

    [Test]
    public void Consent_SaysWhatItWritesWhereAndWhatItNeverTrusts()
    {
        var main = Repository("src", "host", "owner", "repo");

        var text = Standing().Consent([Worktree(main, "work", "feature", "repo")]);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain(_config));
            Assert.That(text, Does.Contain($"projects[\"{Spelled(main)}\"].hasTrustDialogAccepted = true"));
            Assert.That(text, Does.Contain(_projects).And.Contain(_clones));
            Assert.That(text, Does.Contain("home folder").And.Contain("drive").And.Contain("holdout"));
            Assert.That(text, Does.Contain("Undo"));
        });
    }

    private StandingFolderTrust Standing() =>
        new(new ClaudeFolderTrust(_config, _undo), _settings, () => [_holdout], Path.Combine(_root, "user"));

    private void SwitchOn() => _settings.Settings = _settings.Settings with { TrustCardFolders = true };

    private bool IsTrusted(string key) =>
        JsonNode.Parse(File.ReadAllText(_config))?["projects"]?[key]?["hasTrustDialogAccepted"]?.GetValue<bool>() == true;

    private string Repository(params string[] parts)
    {
        var folder = Directory.CreateDirectory(Path.Combine([_root, .. parts])).FullName;
        Directory.CreateDirectory(Path.Combine(folder, ".git", "worktrees"));

        return folder;
    }

    private string Worktree(string main, params string[] parts)
    {
        var folder = Directory.CreateDirectory(Path.Combine([_root, .. parts])).FullName;
        var name = Path.GetFileName(Path.GetDirectoryName(folder))!;
        var gitDir = Directory.CreateDirectory(Path.Combine(main, ".git", "worktrees", name)).FullName;
        File.WriteAllText(Path.Combine(gitDir, "commondir"), "../..");
        File.WriteAllText(Path.Combine(folder, ".git"), $"gitdir: {gitDir}");

        return folder;
    }

    private static string Spelled(string path)
    {
        var forward = path.Replace('\\', '/').TrimEnd('/');

        return char.ToUpperInvariant(forward[0]) + forward[1..];
    }
}
