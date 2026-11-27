using System.Text.Json.Nodes;
using Theseus.Core.Domain;
using Theseus.Core.Launching;

namespace Theseus.Tests.Launching;

/// <summary>
/// A card with worktrees starts in its workstream folder, the plain folder that holds them, with its records folder added
/// and no worktree added (objective 1.3f, Nigel 2026-11-27): an added worktree is keyed by its main clone, which was never
/// trusted, so every such card asked.
/// </summary>
[TestFixture]
public class StartFoldersTests
{
    private string _root = null!;
    private string _work = null!;
    private string _records = null!;
    private string _projects = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "theseus-start-" + Guid.NewGuid().ToString("N"));
        _work = Directory.CreateDirectory(Path.Combine(_root, "work")).FullName;
        var hq = Directory.CreateDirectory(Path.Combine(_root, "hq")).FullName;
        Directory.CreateDirectory(Path.Combine(hq, ".git"));
        _records = Directory.CreateDirectory(Path.Combine(hq, "notes")).FullName;
        _projects = Path.Combine(_root, "claude", "projects");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void ACardWithAWorktree_StartsInItsWorkstreamFolder_AddingOnlyItsRecordsFolder()
    {
        var worktree = Worktree("feature", "repo");

        var arranged = StartFolders.Arrange(Card(_records, worktree), _work, null, _projects).Workstream;

        Assert.Multiple(() =>
        {
            Assert.That(arranged.WorkingDirectory, Is.EqualTo(Path.Combine(_work, "feature")));
            Assert.That(arranged.AddDirectories, Is.EqualTo(new[] { _records }), "never a worktree");
        });
    }

    [Test]
    public void ACardWithSeveralRepositories_StartsInTheOneFolderHoldingThemAll()
    {
        var first = Worktree("feature", "one");
        var second = Worktree("feature", "two");

        var arranged = StartFolders.Arrange(Card(_records, first, second), _work, null, _projects).Workstream;

        Assert.Multiple(() =>
        {
            Assert.That(arranged.WorkingDirectory, Is.EqualTo(Path.Combine(_work, "feature")));
            Assert.That(arranged.AddDirectories, Is.EqualTo(new[] { _records }));
            Assert.That(arranged.StartInstruction, Does.Contain(first).And.Contain(second));
        });
    }

    [Test]
    public void TheOpening_SaysWhereTheRepositoriesAre_AndToReadTheirInstructions()
    {
        var worktree = Worktree("feature", "repo");

        var arranged = StartFolders.Arrange(Card(_records, worktree) with { StartInstruction = "Earlier." }, _work, null, _projects).Workstream;

        Assert.Multiple(() =>
        {
            Assert.That(arranged.StartInstruction, Does.StartWith("Earlier."));
            Assert.That(arranged.StartInstruction, Does.Contain(worktree).And.Contain("CLAUDE.md").And.Contain("AGENTS.md"));
        });
    }

    [Test]
    public void OtherAddedFolders_AreKept()
    {
        var worktree = Worktree("feature", "repo");
        var other = Directory.CreateDirectory(Path.Combine(_root, "elsewhere")).FullName;

        var arranged = StartFolders.Arrange(Card(_records, worktree, other), _work, null, _projects).Workstream;

        Assert.That(arranged.AddDirectories, Is.EqualTo(new[] { _records, other }));
    }

    [Test]
    public void ACardWithNoWorktree_IsLeftAsItIs()
    {
        var other = Directory.CreateDirectory(Path.Combine(_root, "elsewhere")).FullName;
        var card = Card(_records, other);

        var arranged = StartFolders.Arrange(card, _work, null, _projects);

        Assert.Multiple(() =>
        {
            Assert.That(arranged.Workstream, Is.SameAs(card));
            Assert.That(arranged.MemoryDirectory, Is.Null);
        });
    }

    [Test]
    public void ACardWithWorktreesOfTwoWorkstreams_IsLeftAsItIs()
    {
        var card = Card(_records, Worktree("one", "repo"), Worktree("two", "repo"));

        Assert.That(StartFolders.Arrange(card, _work, null, _projects).Workstream, Is.SameAs(card));
    }

    [Test]
    public void AWorkstreamFolderThatIsARepository_IsNotStartedIn()
    {
        var worktree = Worktree("feature", "repo");
        Directory.CreateDirectory(Path.Combine(_work, "feature", ".git"));
        var card = Card(_records, worktree);

        Assert.That(StartFolders.Arrange(card, _work, null, _projects).Workstream, Is.SameAs(card));
    }

    [Test]
    public void WithNoWorktreesFolder_TheCardIsLeftAsItIs()
    {
        var card = Card(_records, Worktree("feature", "repo"));

        Assert.That(StartFolders.Arrange(card, string.Empty, null, _projects).Workstream, Is.SameAs(card));
    }

    [Test]
    public void TheMemory_IsTheRecordsFoldersRepositorys_SoTheSessionKeepsIt()
    {
        var arranged = StartFolders.Arrange(Card(_records, Worktree("feature", "repo")), _work, null, _projects);

        Assert.That(arranged.MemoryDirectory, Is.EqualTo(Path.Combine(_projects, StartFolders.ProjectFolderName(Path.Combine(_root, "hq")), "memory")));
    }

    [Test]
    public void AResumeRecordedUnderTheRecordsFolder_StaysThere_AddingTheWorkstreamFolderNotTheWorktree()
    {
        var worktree = Worktree("feature", "repo");

        var arranged = StartFolders.Arrange(Card(_records, worktree), _work, StartFolders.ProjectFolderName(_records), _projects);

        Assert.Multiple(() =>
        {
            Assert.That(arranged.Workstream.WorkingDirectory, Is.EqualTo(_records), "Claude Code finds a session by the folder it started in");
            Assert.That(arranged.Workstream.AddDirectories, Is.EqualTo(new[] { Path.Combine(_work, "feature") }));
            Assert.That(arranged.MemoryDirectory, Is.Null);
            Assert.That(arranged.Workstream.StartInstruction, Does.Contain($"starts in {_records}"));
        });
    }

    [Test]
    public void AResumeRecordedAnywhereElse_StaysInTheCardsOwnFolder()
    {
        var arranged = StartFolders.Arrange(Card(_records, Worktree("feature", "repo")), _work, "a-name-Claude-Code-shortened", _projects);

        Assert.That(arranged.Workstream.WorkingDirectory, Is.EqualTo(_records), "a guess that moves it would lose the conversation");
    }

    [Test]
    public void ACardWithNoFolder_IsLeftAsItIs()
    {
        var card = Card(string.Empty, Worktree("feature", "repo"));

        Assert.That(StartFolders.Arrange(card, _work, null, _projects).Workstream, Is.SameAs(card));
    }

    [Test]
    public void AResumeRecordedUnderTheWorkstreamFolder_StartsThere()
    {
        var worktree = Worktree("feature", "repo");

        var arranged = StartFolders.Arrange(Card(_records, worktree), _work, StartFolders.ProjectFolderName(Path.Combine(_work, "feature")), _projects);

        Assert.That(arranged.Workstream.WorkingDirectory, Is.EqualTo(Path.Combine(_work, "feature")));
    }

    [Test]
    public void TheProjectFolderName_IsClaudeCodesSpelling()
    {
        Assert.That(StartFolders.ProjectFolderName(@"E:\claude-hq\notes"), Is.EqualTo("E--claude-hq-notes"));
    }

    [Test]
    public void TheMemorySetting_IsAddedToTheSessionsSettings_KeepingWhatWasThere()
    {
        var settings = JsonNode.Parse(SessionMemorySettings.With("{\"hooks\":{}}", @"C:\memory")!)!;

        Assert.Multiple(() =>
        {
            Assert.That(settings["autoMemoryDirectory"]?.GetValue<string>(), Is.EqualTo(@"C:\memory"));
            Assert.That(settings["hooks"], Is.Not.Null);
            Assert.That(SessionMemorySettings.With("{}", null), Is.EqualTo("{}"));
            Assert.That(JsonNode.Parse(SessionMemorySettings.With(null, @"C:\memory")!)?["autoMemoryDirectory"]?.GetValue<string>(), Is.EqualTo(@"C:\memory"));
        });
    }

    private string Worktree(string workstream, string repo)
    {
        var folder = Directory.CreateDirectory(Path.Combine(_work, workstream, repo)).FullName;
        File.WriteAllText(Path.Combine(folder, ".git"), "gitdir: elsewhere");

        return folder;
    }

    private static Workstream Card(string cwd, params string[] added) =>
        new("card", "Card", "#208DD7", false, cwd, added, "Carry on.", 0, "New", false, false, false, null, [], null, DateTimeOffset.UnixEpoch);
}
