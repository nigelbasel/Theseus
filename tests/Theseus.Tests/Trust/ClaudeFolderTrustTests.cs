using System.Text.Json.Nodes;
using Theseus.Core.Trust;

namespace Theseus.Tests.Trust;

/// <summary>
/// Pre-trusting the folders Theseus starts sessions in (project-view-usability item 7, Nigel 2026-11-23): Claude Code
/// asked to trust every added folder on every start, because its answer to an added folder lasts one session.
/// </summary>
[TestFixture]
public class ClaudeFolderTrustTests
{
    private string _root = null!;
    private string _config = null!;
    private string _undo = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "theseus-trust-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _config = Path.Combine(_root, "config.json");
        _undo = Path.Combine(_root, "undo.json");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Needs_KeysAWorktreeByItsMainRepository()
    {
        var main = Repository("src", "repo");
        var worktree = Worktree(main, "work", "feature", "repo");
        Config(new JsonObject());

        var needs = Trust().Needs([worktree]);

        Assert.That(needs.Select(need => need.Key), Is.EqualTo(new[] { Spelled(main) }),
            "Claude Code records a worktree's trust against the repository it belongs to");
    }

    [Test]
    public void Needs_GivesOneNeedForTwoWorktreesOfOneRepository()
    {
        var main = Repository("src", "repo");
        Config(new JsonObject());

        var needs = Trust().Needs([Worktree(main, "work", "one", "repo"), Worktree(main, "work", "two", "repo")]);

        Assert.That(needs, Has.Count.EqualTo(1));
    }

    [Test]
    public void Needs_LeavesOutAFolderTrustedThroughAParentOutsideAnyRepository()
    {
        var notes = Directory.CreateDirectory(Path.Combine(_root, "hq", "notes")).FullName;
        Config(Projects((Spelled(Path.Combine(_root, "hq")), true)));

        Assert.That(Trust().Needs([notes]), Is.Empty);
    }

    [Test]
    public void Needs_DoesNotInheritAParentsTrustAcrossARepositorysRoot()
    {
        var main = Repository("trusted", "repo");
        Config(Projects((Spelled(Path.Combine(_root, "trusted")), true)));

        Assert.That(Trust().Needs([main]).Select(need => need.Key), Is.EqualTo(new[] { Spelled(main) }),
            "Claude Code's lookup stops at the repository's root, so a trusted parent does not cover it");
    }

    [Test]
    public void Needs_LeavesOutAMissingFolder()
    {
        Config(new JsonObject());

        Assert.That(Trust().Needs([Path.Combine(_root, "gone")]), Is.Empty);
    }

    [Test]
    public void Trust_SetsTheFlagAndKeepsEverythingElseInTheFile()
    {
        var main = Repository("src", "repo");
        var other = Projects((Spelled(main), false));
        other["numStartups"] = 42;
        ((JsonObject)other["projects"]![Spelled(main)]!)["lastCost"] = 1.5;
        Config(other);
        var trust = Trust();

        var outcome = trust.Trust(trust.Needs([main]));
        var written = Read();

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True, outcome.Message);
            Assert.That(written["numStartups"]!.GetValue<int>(), Is.EqualTo(42));
            Assert.That(written["projects"]![Spelled(main)]!["hasTrustDialogAccepted"]!.GetValue<bool>(), Is.True);
            Assert.That(written["projects"]![Spelled(main)]!["lastCost"]!.GetValue<double>(), Is.EqualTo(1.5));
            Assert.That(trust.Needs([main]), Is.Empty);
        });
    }

    [Test]
    public void Change_NamesTheFileAndEveryRecordBeforeAnythingIsWritten()
    {
        var main = Repository("src", "repo");
        var worktree = Worktree(main, "work", "feature", "repo");
        Config(new JsonObject());
        var before = File.ReadAllText(_config);
        var trust = Trust();

        var change = trust.Change(trust.Needs([worktree]));

        Assert.Multiple(() =>
        {
            Assert.That(change, Does.Contain(_config));
            Assert.That(change, Does.Contain($"projects[\"{Spelled(main)}\"].hasTrustDialogAccepted = true"));
            Assert.That(change, Does.Contain(worktree));
            Assert.That(File.ReadAllText(_config), Is.EqualTo(before));
        });
    }

    [Test]
    public void Undo_RemovesARecordItCreatedAndClearsTheFlagItSetOnAnother()
    {
        var created = Repository("src", "new");
        var existing = Repository("src", "old");
        Config(Projects((Spelled(existing), false)));
        var trust = Trust();
        trust.Trust(trust.Needs([created, existing]));

        var outcome = trust.Undo();
        var projects = (JsonObject)Read()["projects"]!;

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.True, outcome.Message);
            Assert.That(projects.ContainsKey(Spelled(created)), Is.False);
            Assert.That(projects[Spelled(existing)]!["hasTrustDialogAccepted"]!.GetValue<bool>(), Is.False);
            Assert.That(trust.CanUndo, Is.False);
        });
    }

    [Test]
    public void Undo_KeepsARecordASessionHasWrittenIntoSince()
    {
        var created = Repository("src", "new");
        Config(new JsonObject());
        var trust = Trust();
        trust.Trust(trust.Needs([created]));
        var written = Read();
        ((JsonObject)written["projects"]![Spelled(created)]!)["lastSessionId"] = "a-session";
        File.WriteAllText(_config, written.ToJsonString());

        trust.Undo();

        Assert.That(Read()["projects"]![Spelled(created)]!["hasTrustDialogAccepted"]!.GetValue<bool>(), Is.False);
    }

    [Test]
    public void Trust_ChangesNothingWhenTheFileCannotBeRead()
    {
        var main = Repository("src", "repo");
        File.WriteAllText(_config, "{ not json");

        var outcome = Trust().Trust([new FolderTrustNeed(main, Spelled(main))]);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Succeeded, Is.False);
            Assert.That(File.ReadAllText(_config), Is.EqualTo("{ not json"));
        });
    }

    private ClaudeFolderTrust Trust() => new(_config, _undo);

    private JsonObject Read() => (JsonObject)JsonNode.Parse(File.ReadAllText(_config))!;

    private void Config(JsonObject root) => File.WriteAllText(_config, root.ToJsonString());

    private static JsonObject Projects(params (string Key, bool Trusted)[] entries)
    {
        var projects = new JsonObject();

        foreach (var (key, trusted) in entries)
        {
            projects[key] = new JsonObject { ["hasTrustDialogAccepted"] = trusted };
        }

        return new JsonObject { ["projects"] = projects };
    }

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

    private static string Spelled(string path) => path.Replace('\\', '/').TrimEnd('/');
}
