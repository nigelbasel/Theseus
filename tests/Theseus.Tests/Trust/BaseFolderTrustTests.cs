using System.Text.Json.Nodes;
using Theseus.Core.Settings;
using Theseus.Core.Trust;
using Theseus.Tests.ViewModels;

namespace Theseus.Tests.Trust;

/// <summary>
/// The base folders Theseus keeps trusted (objective 1.3f, Nigel 2026-11-27: "We want to have the base directories,
/// claude-hq, and work trusted, so that all subdirectories are also trusted"): one plain-folder record each, set once, never
/// a home folder, a drive's root, a holdout or a folder inside a repository, and taken back by Undo.
/// </summary>
[TestFixture]
public class BaseFolderTrustTests
{
    private string _root = null!;
    private string _config = null!;
    private string _undo = null!;
    private string _hq = null!;
    private string _projects = null!;
    private string _work = null!;
    private string _holdout = null!;
    private MemorySettingsStore _settings = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "theseus-base-" + Guid.NewGuid().ToString("N"));
        _config = Path.Combine(Directory.CreateDirectory(_root).FullName, "config.json");
        _undo = Path.Combine(_root, "undo.json");
        _hq = Directory.CreateDirectory(Path.Combine(_root, "hq")).FullName;
        Directory.CreateDirectory(Path.Combine(_hq, ".git"));
        _projects = Directory.CreateDirectory(Path.Combine(_hq, "Proposals")).FullName;
        _work = Directory.CreateDirectory(Path.Combine(_root, "work")).FullName;
        _holdout = Directory.CreateDirectory(Path.Combine(_root, "holdouts")).FullName;
        File.WriteAllText(_config, new JsonObject().ToJsonString());
        _settings = new MemorySettingsStore
        {
            Settings = TheseusSettings.Default with { ProjectsFolder = _projects, WorktreesFolder = _work, BaseFolders = [_hq, _work] },
        };
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void TheDefaults_AreTheProjectsFoldersRepository_AndTheWorktreesFolder()
    {
        Assert.That(BaseFolderTrust.Defaults(_settings.Settings), Is.EqualTo(new[] { _hq, _work }));
    }

    [Test]
    public void TheDefaults_LeaveOutAFolderThatIsNotSet()
    {
        Assert.That(BaseFolderTrust.Defaults(_settings.Settings with { WorktreesFolder = string.Empty }), Is.EqualTo(new[] { _hq }));
    }

    [Test]
    public void Keep_WritesOnePlainRecordForEachBaseFolder()
    {
        var outcome = Base().Keep();

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Trusted, Is.EqualTo(new[] { Spelled(_hq), Spelled(_work) }));
            Assert.That(IsTrusted(Spelled(_hq)) && IsTrusted(Spelled(_work)), Is.True);
            Assert.That(Projects().Count, Is.EqualTo(2), "no record for any repository or worktree under them");
        });
    }

    [Test]
    public void Keep_WritesNoRecordForTheRepositoriesUnderABaseFolder()
    {
        var worktree = Directory.CreateDirectory(Path.Combine(_work, "feature", "repo")).FullName;
        File.WriteAllText(Path.Combine(worktree, ".git"), "gitdir: elsewhere");

        Base().Keep();

        Assert.That(Projects().Select(entry => entry.Key), Is.EquivalentTo(new[] { Spelled(_hq), Spelled(_work) }));
    }

    [Test]
    public void Keep_WritesOnlyWhatIsMissing()
    {
        Base().Keep();

        var again = Base().Keep();

        Assert.That(again.Trusted, Is.Empty);
    }

    [Test]
    public void Keep_LeavesARecordTheOwnerGaveAlone()
    {
        var root = (JsonObject)JsonNode.Parse(File.ReadAllText(_config))!;
        root["projects"] = new JsonObject { [Spelled(_hq)] = new JsonObject { ["hasTrustDialogAccepted"] = true, ["allowedTools"] = new JsonArray("Bash") } };
        File.WriteAllText(_config, root.ToJsonString());

        var outcome = Base().Keep();

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Trusted, Is.EqualTo(new[] { Spelled(_work) }));
            Assert.That(Projects()[Spelled(_hq)]?["allowedTools"]?.AsArray().Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void Keep_BeforeAnyBaseFolderIsSet_WritesNothing()
    {
        _settings.Settings = _settings.Settings with { BaseFolders = null };

        Assert.Multiple(() =>
        {
            Assert.That(Base().Keep().Trusted, Is.Empty, "the defaults are not the consent until they are set");
            Assert.That(File.Exists(_undo), Is.False);
        });
    }

    [Test]
    public void Keep_WithNoBaseFolders_WritesNothing()
    {
        _settings.Settings = _settings.Settings with { BaseFolders = [] };

        var outcome = Base().Keep();

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Trusted, Is.Empty);
            Assert.That(File.Exists(_undo), Is.False);
        });
    }

    [Test]
    public void Keep_NeverTrustsTheHomeFolder_OrAFolderHoldingIt()
    {
        var home = Directory.CreateDirectory(Path.Combine(_work, "me")).FullName;
        _settings.Settings = _settings.Settings with { BaseFolders = [home, _work] };

        var outcome = new BaseFolderTrust(new ClaudeFolderTrust(_config, _undo), _settings, () => [], home).Keep();

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Trusted, Is.Empty);
            Assert.That(outcome.Refused, Is.EquivalentTo(new[] { home, _work }));
        });
    }

    [Test]
    public void Keep_NeverTrustsADrivesRoot()
    {
        var drive = Path.GetPathRoot(_root)!;
        _settings.Settings = _settings.Settings with { BaseFolders = [drive] };

        Assert.That(Base().Keep().Trusted, Is.Empty);
    }

    [Test]
    public void Keep_NeverTrustsAHoldout_AFolderInOne_OrAFolderHoldingOne()
    {
        var inside = Directory.CreateDirectory(Path.Combine(_holdout, "scenarios")).FullName;
        var holding = Directory.CreateDirectory(Path.Combine(_root, "holding")).FullName;
        var held = Directory.CreateDirectory(Path.Combine(holding, "checks")).FullName;
        _settings.Settings = _settings.Settings with { BaseFolders = [_holdout, inside, holding] };

        var outcome = new BaseFolderTrust(new ClaudeFolderTrust(_config, _undo), _settings, () => [_holdout, held], Path.Combine(_root, "user")).Keep();

        Assert.That(outcome.Trusted, Is.Empty);
    }

    [Test]
    public void Keep_NeverTrustsAFolderNamedAsAHoldout()
    {
        var named = Directory.CreateDirectory(Path.Combine(_root, "repo" + Theseus.Core.Links.HomeRepo.HoldoutSuffix)).FullName;
        _settings.Settings = _settings.Settings with { BaseFolders = [named] };

        Assert.That(Base().Keep().Trusted, Is.Empty);
    }

    [Test]
    public void Keep_NeverTrustsAFolderInsideARepository_WhichWouldTrustTheRepositoryWhole()
    {
        _settings.Settings = _settings.Settings with { BaseFolders = [_projects] };

        var outcome = Base().Keep();

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Trusted, Is.Empty);
            Assert.That(IsTrusted(Spelled(_hq)), Is.False);
        });
    }

    [Test]
    public void Add_RefusesAFolderThatMayNeverBeABaseFolder_AndAddsOneThatMay()
    {
        var other = Directory.CreateDirectory(Path.Combine(_root, "other")).FullName;
        var trust = Base();

        _settings.Settings = _settings.Settings with { BaseFolders = null };
        trust.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(trust.Refusal(_projects), Does.Contain("inside the repository"));
            Assert.That(trust.Refusal(other), Is.Null);
            Assert.That(trust.Add(other), Is.True);
            Assert.That(trust.Folders, Is.EqualTo(new[] { _hq, _work, other }));
        });
    }

    [Test]
    public void SetDefaultsOnUpgrade_SetsThemOnce_ForAnInstallWithFoldersChosen()
    {
        _settings.Settings = _settings.Settings with { BaseFolders = null };
        var trust = Base();

        Assert.Multiple(() =>
        {
            Assert.That(trust.SetDefaultsOnUpgrade(), Is.True);
            Assert.That(_settings.Settings.BaseFolders, Is.EqualTo(new[] { _hq, _work }));
            Assert.That(trust.SetDefaultsOnUpgrade(), Is.False, "the banner is shown once");
        });
    }

    [Test]
    public void SetDefaultsOnUpgrade_DoesNothing_OnAnInstallWithNoFoldersChosen()
    {
        _settings.Settings = TheseusSettings.Default;

        Assert.Multiple(() =>
        {
            Assert.That(Base().SetDefaultsOnUpgrade(), Is.False);
            Assert.That(_settings.Settings.BaseFolders, Is.Null);
        });
    }

    [Test]
    public void Undo_TakesBackWhatItSet_AndEmptiesTheBaseFolders()
    {
        var root = (JsonObject)JsonNode.Parse(File.ReadAllText(_config))!;
        root["projects"] = new JsonObject { ["E:/owners-own"] = new JsonObject { ["hasTrustDialogAccepted"] = true } };
        File.WriteAllText(_config, root.ToJsonString());
        var trust = Base();
        trust.Keep();

        var outcome = trust.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True, outcome.Message);
            Assert.That(IsTrusted(Spelled(_hq)) || IsTrusted(Spelled(_work)), Is.False);
            Assert.That(IsTrusted("E:/owners-own"), Is.True, "a record the owner gave himself is not Theseus's to take back");
            Assert.That(trust.Folders, Is.Empty, "otherwise the next start would trust them again");
            Assert.That(trust.Keep().Trusted, Is.Empty);
        });
    }

    [Test]
    public void TheExplanation_SaysWhatItWritesWhere_AndWhatItNeverTrusts()
    {
        var text = Base().Explanation();

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain(_config).And.Contain("hasTrustDialogAccepted = true"));
            Assert.That(text, Does.Contain("home folder").And.Contain("drive").And.Contain("holdout").And.Contain("repository"));
            Assert.That(text, Does.Contain("Undo"));
        });
    }

    private BaseFolderTrust Base() => new(new ClaudeFolderTrust(_config, _undo), _settings, () => [_holdout], Path.Combine(_root, "user"));

    private JsonObject Projects() => JsonNode.Parse(File.ReadAllText(_config))?["projects"] as JsonObject ?? [];

    private bool IsTrusted(string key) => Projects()[key]?["hasTrustDialogAccepted"]?.GetValue<bool>() == true;

    private static string Spelled(string path)
    {
        var forward = path.Replace('\\', '/').TrimEnd('/');

        return char.ToUpperInvariant(forward[0]) + forward[1..];
    }
}
