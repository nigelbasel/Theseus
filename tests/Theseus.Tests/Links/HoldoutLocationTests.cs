using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Links;

namespace Theseus.Tests.Links;

/// <summary>
/// The holdout folder by convention (1.1e AC 6c): a folder per project under Theseus's data folder unless the owner chose
/// one, created on first use; whether it has scenarios; the scenario writer's role and card; Options showing the folder in
/// use.
/// </summary>
[TestFixture]
public class HoldoutLocationTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "theseus-holdout-location-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        // Git writes its objects read-only, so a folder made a repository is cleared of that first.
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void WithNoFolderChosen_EachProjectHasItsOwnDefault_AndAdHocCardsOneTogether()
    {
        var theseus = HoldoutLocation.For("", "theseus", _root);

        Assert.That(theseus, Is.EqualTo(new HoldoutLocation(Path.Combine(_root, "Theseus", "holdouts", "theseus"), true)));
        Assert.That(HoldoutLocation.For(null, null, _root).Folder, Is.EqualTo(Path.Combine(_root, "Theseus", "holdouts", "ad-hoc")));
        Assert.That(theseus.IsMissing, Is.False, "a default is made on first use, never missing");
    }

    [Test]
    public void AFolderTheOwnerChose_IsKept_AndIsMissingWhenItIsNotThere()
    {
        var chosen = HoldoutLocation.For(@"  C:\no\such\holdout ", "theseus", _root);

        Assert.That((chosen.Folder, chosen.IsDefault, chosen.IsMissing), Is.EqualTo((@"C:\no\such\holdout", false, true)));
    }

    [Test]
    public void Ensure_MakesTheDefault_ButNeverAChosenFolder()
    {
        var theseus = HoldoutLocation.For("", "theseus", _root);
        var chosen = new HoldoutLocation(Path.Combine(_root, "chosen"), false);

        theseus.Ensure();
        chosen.Ensure();

        Assert.That((Directory.Exists(theseus.Folder), Directory.Exists(chosen.Folder)), Is.EqualTo((true, false)));
    }

    [Test]
    public void Scenarios_AreFilesOutsideTheResultsFolderAndGit()
    {
        var holdout = new HoldoutLocation(Path.Combine(_root, "h"), true);

        Assert.That(holdout.HasScenarios, Is.False, "no folder yet");

        Directory.CreateDirectory(Path.Combine(holdout.Folder, "results"));
        Directory.CreateDirectory(Path.Combine(holdout.Folder, ".git"));
        File.WriteAllText(Path.Combine(holdout.Folder, "results", "report.md"), "x");
        File.WriteAllText(Path.Combine(holdout.Folder, ".git", "HEAD"), "x");
        File.WriteAllText(Path.Combine(holdout.Folder, ".gitignore"), "x");

        Assert.That(holdout.HasScenarios, Is.False, "reports and the repository's own files are not scenarios");

        Directory.CreateDirectory(Path.Combine(holdout.Folder, "board"));
        File.WriteAllText(Path.Combine(holdout.Folder, "board", "stop.md"), "x");

        Assert.That(holdout.HasScenarios, Is.True);
    }

    [Test]
    public void AScenarioWriter_RunsAsTheHoldoutsAuthor_AndWritesScenariosOnly()
    {
        var writer = ScenarioWriterCard.For("Theseus", @"<root>\holdouts\theseus", @"<projects>\theseus");

        Assert.That(RoleEnvironment.For(WorkstreamRole.ScenarioWriter)[RoleEnvironment.HoldoutAuthorVariable], Is.EqualTo("1"));
        Assert.That(RoleEnvironment.For(WorkstreamRole.None), Is.Empty, "a building session never has it");
        Assert.That((writer.Title, writer.WorkingDirectory, writer.AddDirectories),
            Is.EqualTo(("Write holdout scenarios for Theseus", @"<root>\holdouts\theseus", new[] { @"<projects>\theseus" })));
        Assert.That(writer.Prompt, Does.Contain("you write scenarios, never code"));
    }

    [Test]
    public void Options_SaysWhatTheFolderForEveryProjectDoes()
    {
        var store = new Theseus.Core.Settings.SettingsStore(Path.Combine(_root, "settings.json"));
        var settings = new SettingsViewModel(store, () => { });

        Assert.That(settings.HoldoutFolderInUse, Is.EqualTo("None: each project's folder is found by convention, listed below"));

        settings.HoldoutFolder = @"C:\clones\holdout";

        Assert.That(settings.HoldoutFolderInUse, Is.EqualTo(@"C:\clones\holdout, for every project that has no folder of its own"));
    }

    [Test]
    public void TheOrder_ChosenForTheProject_ThenForAll_ThenBesideTheRepo_ThenTheseussOwn()
    {
        var beside = Path.Combine(_root, "Theseus-holdout");

        Assert.That(HoldoutLocation.Find("theseus", @"C:\mine", @"C:\all", beside, _root),
            Is.EqualTo(new HoldoutLocation(@"C:\mine", false) { Source = HoldoutSource.Chosen }));
        Assert.That(HoldoutLocation.Find("theseus", null, @"C:\all", beside, _root).Folder, Is.EqualTo(@"C:\all"));
        Assert.That(HoldoutLocation.Find("theseus", null, "", beside, _root),
            Is.EqualTo(new HoldoutLocation(beside, false) { Source = HoldoutSource.BesideRepo }));
        Assert.That(HoldoutLocation.Find("theseus", null, "", null, _root),
            Is.EqualTo(new HoldoutLocation(Path.Combine(_root, "Theseus", "holdouts", "theseus"), true)));
        Assert.That(HoldoutLocation.Find(null, null, "", beside, _root).Folder, Is.EqualTo(Path.Combine(_root, "Theseus", "holdouts", "ad-hoc")),
            "ad hoc cards have no repo to sit beside");
    }

    [Test]
    public void AFolderBesideTheHomeReposClone_IsFound_ForAFixtureRepo()
    {
        var clone = Path.Combine(_root, "src", "Theseus");
        var worktree = Path.Combine(clone, "src", "Theseus.App");
        Directory.CreateDirectory(worktree);
        Directory.CreateDirectory(Path.Combine(clone, ".git"));
        const string project = "# Theseus\n\n## Repos\n| Repo | Role | Autonomy |\n|---|---|---|\n| github/owner/Theseus | home | L0 |\n| github/owner/Other | changed | L0 |\n";

        Assert.That(HomeRepo.NameIn(project), Is.EqualTo("Theseus"));
        Assert.That(HomeRepo.Clone("Theseus", [worktree]), Is.EqualTo(clone));
        Assert.That(HomeRepo.HoldoutBeside(clone), Is.Null, "none there yet");

        Directory.CreateDirectory(Path.Combine(_root, "src", "Theseus-holdout"));

        Assert.That(HomeRepo.HoldoutBeside(clone), Is.EqualTo(Path.Combine(_root, "src", "Theseus-holdout")));
        Assert.That(HomeRepo.NameIn("# T\n\n## Repos\n| Repo | Role | Autonomy |\n|---|---|---|\n| <host>/<owner>/<repo> | home | L0 |\n"), Is.Null,
            "the starter's placeholder is no repo");
    }

    [Test]
    public void TheHomeRepo_IsTheFirstCodeRepo_NotANotesFolderOrAWildcard()
    {
        const string real = "## Repos\n| Repo | Role | Autonomy |\n|---|---|---|\n"
                            + "| local/claude-hq (`<notes>\\buildyourmarket-mcp`) | home | L0 |\n"
                            + "| ado/*: every repository | read | L0 |\n"
                            + "| ado/Core/Nurtur.Config (`<src>\\Nurtur.Config`) | changed | L0 |\n";

        Assert.That(HomeRepo.NameIn(real), Is.EqualTo("Nurtur.Config"));
        Assert.That(HomeRepo.NameIn("## Repos\n| Repo | Role |\n|---|---|\n| local/claude-hq | home |\n"), Is.Null);
    }

    [Test]
    public void AWorktree_LeadsToItsPrimaryClone_BesideWhichTheHoldoutSits()
    {
        var clone = Path.Combine(_root, "src", "Theseus");
        var gitDir = Path.Combine(clone, ".git", "worktrees", "feature");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(gitDir, "commondir"), "../..");
        var worktree = Path.Combine(_root, "work", "feature", "Theseus");
        Directory.CreateDirectory(worktree);
        File.WriteAllText(Path.Combine(worktree, ".git"), $"gitdir: {gitDir}\n");
        Directory.CreateDirectory(Path.Combine(_root, "src", "Theseus-holdout"));

        Assert.That(HomeRepo.Clone("Theseus", [worktree]), Is.EqualTo(clone));
        Assert.That(HomeRepo.HoldoutBeside(HomeRepo.Clone("Theseus", [worktree])), Is.EqualTo(Path.Combine(_root, "src", "Theseus-holdout")));
    }

    [Test]
    public void AFolderMerelyNamedLikeTheRepo_IsNotItsClone()
    {
        var feature = Path.Combine(_root, "work", "theseus", "notes");
        Directory.CreateDirectory(feature);

        Assert.That(HomeRepo.Clone("Theseus", [feature]), Is.Null, "no .git, so no clone");
    }

    [Test]
    public void WithNoRepoNamed_TheCloneIsTheRepositoryTheCardsWorkIn()
    {
        var clone = Path.Combine(_root, "work", "repo");
        Directory.CreateDirectory(Path.Combine(clone, ".git"));
        Directory.CreateDirectory(Path.Combine(clone, "src"));

        Assert.That(HomeRepo.Clone(null, [Path.Combine(clone, "src")]), Is.EqualTo(clone));
    }

    [Test]
    public void TheseussOwnFolder_IsMadeALocalGitRepository_OnFirstUse()
    {
        var own = HoldoutLocation.Find("theseus", null, "", null, _root);

        Assert.That(own.Ensure(), Is.True);
        Assert.That(Directory.Exists(Path.Combine(own.Folder, ".git")), Is.True);
        var head = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", "rev-parse --verify HEAD")
        {
            WorkingDirectory = own.Folder,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;
        head.StandardOutput.ReadToEnd();
        head.WaitForExit();
        Assert.That(head.ExitCode, Is.EqualTo(0), "it has a first commit");
        Assert.That(own.Ensure(), Is.True, "made once");
        Assert.That(new HoldoutLocation(Path.Combine(_root, "chosen"), false) { Source = HoldoutSource.Chosen }.Ensure(), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_root, "chosen")), Is.False, "a chosen folder is never created");
    }

    [Test]
    public void TheFoldersInForce_AreWritten_OnlyWhenTheyChange()
    {
        var path = HoldoutFolderFile.PathIn(_root);
        IReadOnlyList<HoldoutFolderEntry> folders = [new("theseus", @"C:\src\Theseus-holdout", HoldoutSource.BesideRepo)];

        Assert.That((HoldoutFolderFile.Write(path, folders), HoldoutFolderFile.Write(path, folders)), Is.EqualTo((true, false)));
        Assert.That(File.ReadAllText(path), Does.Contain("Theseus-holdout"));

        HoldoutFolderFile.Write(path, [new HoldoutFolderEntry("theseus", @"C:\holdouts\theseus", HoldoutSource.TheseusOwn)]);

        Assert.That(HoldoutFolderFile.Read(path).Select(entry => entry.Folder), Is.EquivalentTo(new[] { @"C:\holdouts\theseus", @"C:\src\Theseus-holdout" }),
            "a folder once listed stays guarded when the convention changes");
        Assert.That(folders[0].HowFound, Is.EqualTo("found beside the repo"));
    }

    [Test]
    public void TheChecks_ListEachProjectsFolder_FoundInOrder()
    {
        var checks = new HoldoutChecks(_root);
        var beside = Path.Combine(_root, "Theseus-holdout");
        checks.Refresh("", new Dictionary<string, string> { ["bym"] = @"C:\mine" }, project => project == "theseus" ? beside : null,
            [Card("a", "theseus"), Card("b", "bym"), Card("c", null), Card("d", "pd-70")]);

        Assert.That(checks.Entries.Select(entry => (entry.Project, entry.Source)), Is.EqualTo(new[]
        {
            ("ad-hoc", HoldoutSource.TheseusOwn), ("bym", HoldoutSource.Chosen), ("pd-70", HoldoutSource.TheseusOwn), ("theseus", HoldoutSource.BesideRepo),
        }));
    }

    [Test]
    public void Options_TheAdHocRowCannotBeChanged_AResetRowSaysWhatComes_AndADriveIsRefused()
    {
        var store = new Theseus.Core.Settings.SettingsStore(Path.Combine(_root, "settings.json"));
        store.Write(store.Read() with { ProjectHoldoutFolders = new Dictionary<string, string> { ["bym"] = @"C:\mine" } });
        var settings = new SettingsViewModel(store, () => { });
        settings.OpenCommand.Execute(null);
        settings.HoldoutEntries = [new("ad-hoc", @"C:\h\ad-hoc", HoldoutSource.TheseusOwn), new("bym", @"C:\mine", HoldoutSource.Chosen)];

        Assert.That((settings.HoldoutRows[0].CanChange, settings.HoldoutRows[0].ChangeToolTip), Is.EqualTo((false, "Cards in no project always use Theseus's own ad-hoc folder")));
        Assert.That(settings.HoldoutRows[0].IsTheseusOwn, Is.True, "it can be kept in a private repository");

        settings.ResetProjectHoldoutCommand.Execute(settings.HoldoutRows[1]);

        Assert.That(settings.HoldoutRows[1].Line, Is.EqualTo("found by convention once saved"));
        Assert.That(settings.ChooseProjectHoldout("bym", @"C:\"), Does.StartWith("A whole drive"));
        Assert.That(settings.ChooseProjectHoldout("bym", "relative"), Does.StartWith("Choose a full path"));

        settings.SaveCommand.Execute(null);

        Assert.That(store.Read().ProjectHoldoutFolders, Is.Empty);
    }

    private static Workstream Card(string key, string? project) =>
        new Workstream(key, key, "#208DD7", false, @"C:\work\" + key, [], "Carry on.", 0, "New", false, false, false, null, [], null,
            DateTimeOffset.UnixEpoch) with { Project = project };
}
