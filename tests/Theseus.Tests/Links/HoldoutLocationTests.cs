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
        var theseus = HoldoutLocation.Find("theseus", null, null, _root);

        Assert.That(theseus, Is.EqualTo(new HoldoutLocation(Path.Combine(_root, "Theseus", "holdouts", "theseus"), true)));
        Assert.That(HoldoutLocation.Find(null, null, null, _root).Folder, Is.EqualTo(Path.Combine(_root, "Theseus", "holdouts", "ad-hoc")));
        Assert.That(theseus.IsMissing, Is.False, "a default is made on first use, never missing");
    }

    [Test]
    public void AFolderTheOwnerChose_IsKept_AndIsMissingWhenItIsNotThere()
    {
        var chosen = HoldoutLocation.Find("theseus", @"  C:\no\such\holdout ", null, _root);

        Assert.That((chosen.Folder, chosen.IsDefault, chosen.IsMissing), Is.EqualTo((@"C:\no\such\holdout", false, true)));
    }

    [Test]
    public void Ensure_MakesTheDefault_ButNeverAChosenFolder()
    {
        var theseus = HoldoutLocation.Find("theseus", null, null, _root);
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
        Assert.That(writer.Prompt, Does.Contain("a line \"Checks: <slug>, <slug>\" naming each piece of work it checks"),
            "a scenario that names no work makes no verifier (Nigel, 2026-11-23, A2)");
    }

    [Test]
    public void TheWorkAHoldoutsScenariosCheck_IsReadFromTheirChecksLines_NeverFromResults()
    {
        var holdout = Path.Combine(_root, "holdout");
        Directory.CreateDirectory(Path.Combine(holdout, "scenarios"));
        Directory.CreateDirectory(Path.Combine(holdout, VerifierCard.ResultsFolder));
        File.WriteAllText(Path.Combine(holdout, "scenarios", "stop.md"), "# Stop\nChecks: close-step, resumed-stop\n");
        File.WriteAllText(Path.Combine(holdout, "lock.md"), "Checks: guard-mentions");
        File.WriteAllText(Path.Combine(holdout, VerifierCard.ResultsFolder, "report.md"), "Checks: a-report-is-no-scenario");

        Assert.That(new HoldoutLocation(holdout, false).CheckedWork(),
            Is.EquivalentTo(new[] { "close-step", "resumed-stop", "guard-mentions" }));
    }

    [Test]
    public void TheOrder_ChosenForTheProject_ThenRecorded_ThenBesideTheRepo_ThenTheseussOwn()
    {
        var home = Path.Combine(_root, "src", "Theseus");
        var beside = Path.Combine(_root, "src", "Theseus-holdout");
        var named = Path.Combine(_root, "src", "Scenarios");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(beside);
        Directory.CreateDirectory(named);
        var recorded = new HoldoutConvention(home, new RecordedHoldout("Scenarios"));

        Assert.That(HoldoutLocation.Find("theseus", @"C:\mine", recorded, _root),
            Is.EqualTo(new HoldoutLocation(@"C:\mine", false) { Source = HoldoutSource.Chosen }));
        Assert.That(HoldoutLocation.Find("theseus", null, recorded, _root),
            Is.EqualTo(new HoldoutLocation(named, false) { Source = HoldoutSource.Recorded }),
            "the repository project.md names wins over <repo>-holdout and over Theseus's own");
        Assert.That(HoldoutLocation.Find("theseus", null, new HoldoutConvention(home, null), _root),
            Is.EqualTo(new HoldoutLocation(beside, false) { Source = HoldoutSource.BesideRepo }));
        Assert.That(HoldoutLocation.Find("theseus", null, null, _root),
            Is.EqualTo(new HoldoutLocation(Path.Combine(_root, "Theseus", "holdouts", "theseus"), true)));
        Assert.That(HoldoutLocation.Find(null, null, recorded, _root).Folder, Is.EqualTo(Path.Combine(_root, "Theseus", "holdouts", "ad-hoc")),
            "ad hoc cards have no repo to sit beside");
    }

    [Test]
    public void ARecordThatNamesNoRepository_MeansTheRepoHoldoutBesideTheHomeClone_AndSaysItWasRecorded()
    {
        var home = Path.Combine(_root, "src", "Theseus");
        var beside = Path.Combine(_root, "src", "Theseus-holdout");
        Directory.CreateDirectory(home);
        var convention = new HoldoutConvention(home, new RecordedHoldout(null));

        Assert.That(HoldoutLocation.Find("theseus", null, convention, _root).IsMissing, Is.True,
            "recorded but not cloned: missing, never Theseus's own");

        Directory.CreateDirectory(beside);
        var found = HoldoutLocation.Find("theseus", null, convention, _root);

        Assert.That((found.Folder, found.Source, found.IsMissing), Is.EqualTo((beside, HoldoutSource.Recorded, false)));
        Assert.That(HoldoutFolderEntry.HowFoundOf(found.Source), Is.EqualTo("recorded in project.md"));
    }

    [Test]
    public void TheseussOwnProjectFile_RecordsAHoldoutRepository_ButNamesNone()
    {
        const string theseus = "# Theseus\n\n## Repos\n\n| Repo | Role | Autonomy |\n|---|---|---|\n"
                               + "| github.com/nigelbasel/Theseus | code and the method kit | L0 |\n"
                               + "| A private holdout repository | holdout scenarios; never changed by implementing workstreams | owner only |\n";

        Assert.That(HomeRepo.RecordedHoldoutIn(theseus), Is.EqualTo(new RecordedHoldout(null)));
        Assert.That(HomeRepo.NameIn(theseus), Is.EqualTo("Theseus"));
    }

    [Test]
    public void ARecordGivingARepository_NamesIt_AndIsNeverTheHomeRepo()
    {
        const string text = "## Repos\n| Repo | Role | Autonomy |\n|---|---|---|\n"
                            + "| github.com/owner/Repo-scenarios | the holdout scenarios | owner only |\n"
                            + "| github.com/owner/Repo | code | L0 |\n";

        Assert.That(HomeRepo.RecordedHoldoutIn(text), Is.EqualTo(new RecordedHoldout("Repo-scenarios")));
        Assert.That(HomeRepo.NameIn(text), Is.EqualTo("Repo"), "a holdout row first in the table is not the home repo");
    }

    [Test]
    public void ARoleThatMerelyMentionsTheHoldoutGuard_IsNoRecord()
    {
        const string text = "## Repos\n| Repo | Role | Autonomy |\n|---|---|---|\n"
                            + "| github.com/x/Repo | code, the method kit and the holdout guard | L0 |\n";

        Assert.That(HomeRepo.RecordedHoldoutIn(text), Is.Null);
        Assert.That(HomeRepo.NameIn(text), Is.EqualTo("Repo"), "review, finding 4");
    }

    [Test]
    public void TheGuard_StillCoversTheFolderBesideTheRepo_WhenARecordedRepositoryTakesPrecedence()
    {
        var home = Path.Combine(_root, "src", "Foo");
        var beside = Path.Combine(_root, "src", "Foo" + HomeRepo.HoldoutSuffix);
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(beside);
        var checks = new HoldoutChecks(_root);
        checks.Refresh(new Dictionary<string, string>(), _ => new HoldoutConvention(home, new RecordedHoldout("Foo-scenarios")), [Card("a", "foo")]);

        Assert.That(checks.GuardedEntries.Select(entry => entry.Folder),
            Is.EqualTo(new[] { Path.Combine(_root, "src", "Foo-scenarios"), beside }), "review, finding 1");
    }

    [Test]
    public void APieceWhoseReposIsTheHoldoutRepository_IsARecord_AndAProjectWithoutEitherHasNone()
    {
        const string piece = "## Repos\n| Repo | Role | Autonomy |\n|---|---|---|\n| github.com/o/Repo | code | L0 |\n\n"
                             + "### Objective 0.2\n| Workstream | Slug | Branch | Repos | What |\n|---|---|---|---|---|\n"
                             + "| a | holdout-scenarios | x | the holdout repository | Scenarios |\n";

        Assert.That(HomeRepo.RecordedHoldoutIn(piece), Is.EqualTo(new RecordedHoldout(null)));
        Assert.That(HomeRepo.RecordedHoldoutIn("## Repos\n| Repo | Role | Autonomy |\n|---|---|---|\n| github.com/o/Repo | code | L0 |\n"), Is.Null);
    }

    [TestCase("holdout")]
    [TestCase(@"..\holdout")]
    [TestCase(@"\holdout")]
    [TestCase(@"C:\")]
    [TestCase("/c")]
    [TestCase("C:holdout")]
    public void AChosenFolderTheGuardWouldIgnore_IsNotChosen(string typed)
    {
        Assert.That(HoldoutLocation.Find("theseus", typed, null, _root),
            Is.EqualTo(new HoldoutLocation(Path.Combine(_root, "Theseus", "holdouts", "theseus"), true)),
            "re-verify C1 and C2: the guard counts it as unset, so the board must not verify against it");
    }

    [TestCase("/c/holdout")]
    [TestCase("/mnt/c/holdout")]
    public void AChosenFolderTypedTheGitBashWay_IsTheFolderItStandsFor(string typed)
    {
        Assert.That(HoldoutLocation.Find("theseus", typed, null, _root),
            Is.EqualTo(new HoldoutLocation(@"C:\holdout", false) { Source = HoldoutSource.Chosen }),
            "the guard guards C:\\holdout for it, so the board must verify there (review of verifier-runs, M1)");
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
        var own = HoldoutLocation.Find("theseus", null, null, _root);

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
        Assert.That(folders[0].HowFound, Is.EqualTo("beside its repo, by convention"));
    }

    [Test]
    public void TheChecks_ListEachProjectsFolder_FoundInOrder()
    {
        var checks = new HoldoutChecks(_root);
        var home = Path.Combine(_root, "src", "Theseus");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(Path.Combine(_root, "src", "Theseus-holdout"));
        checks.Refresh(new Dictionary<string, string> { ["bym"] = @"C:\mine" },
            project => project == "theseus" ? new HoldoutConvention(home, null) : HoldoutConvention.None,
            [Card("a", "theseus"), Card("b", "bym"), Card("c", null), Card("d", "pd-70")]);

        Assert.That(checks.Entries.Select(entry => (entry.Project, entry.Source)), Is.EqualTo(new[]
        {
            ("ad-hoc", HoldoutSource.TheseusOwn), ("bym", HoldoutSource.Chosen), ("pd-70", HoldoutSource.TheseusOwn), ("theseus", HoldoutSource.BesideRepo),
        }));
    }

    [Test]
    public void Options_ListsEachProjectsFolderReadOnly_AndSavingChangesNoProjectsFolder()
    {
        var store = new Theseus.Core.Settings.SettingsStore(Path.Combine(_root, "settings.json"));
        store.Write(store.Read() with
        {
            HoldoutFolder = @"C:\old\for-every-project",
            ProjectHoldoutFolders = new Dictionary<string, string> { ["bym"] = @"C:\mine" },
        });
        var opened = new List<string>();
        var settings = new SettingsViewModel(store, () => { }) { OpenProjectHoldouts = opened.Add, HasProjectCard = project => project != "gone" };
        settings.OpenCommand.Execute(null);
        settings.HoldoutEntries = [new("ad-hoc", @"C:\h\ad-hoc", HoldoutSource.TheseusOwn), new("bym", @"C:\mine", HoldoutSource.Chosen),
            new("theseus", _root, HoldoutSource.Recorded), new("gone", _root, HoldoutSource.BesideRepo)];

        Assert.That(settings.HoldoutRows.Select(row => (row.Project, row.Value, row.Explanation)), Is.EqualTo(new[]
        {
            ("ad-hoc", @"C:\h\ad-hoc", "Cards in no project always use Theseus's own ad-hoc folder."),
            ("bym", @"C:\mine (not there)", "You chose it, but it is not there. Change it in the project's Project properties."),
            ("theseus", _root, "Recorded in project.md. Change it in the project's Project properties."),
            ("gone", _root, "Beside its repo, by convention. Change it in the project's Project properties."),
        }));
        Assert.That(settings.OpenProjectPropertiesCommand.CanExecute(settings.HoldoutRows[0]), Is.False, "ad hoc cards have no project");
        Assert.That(settings.OpenProjectPropertiesCommand.CanExecute(settings.HoldoutRows[3]), Is.False, "no card to open (review, 7)");

        settings.OpenProjectPropertiesCommand.Execute(settings.HoldoutRows[2]);
        settings.SaveCommand.Execute(null);

        Assert.That(opened, Is.EqualTo(new[] { "theseus" }));
        Assert.That(store.Read().ProjectHoldoutFolders, Is.EqualTo(new Dictionary<string, string> { ["bym"] = @"C:\mine" }),
            "a project's folder is changed only in its Project properties");
        Assert.That(store.Read().HoldoutFolder, Is.EqualTo(@"C:\old\for-every-project"), "kept as it was, and counted for no project");
    }

    [Test]
    public void ProjectProperties_ChangeRefusesADriveOrARelativePath_AndResetSaysWhatComes()
    {
        var own = HoldoutLocation.Find("bym", null, null, _root);
        var properties = new ProjectPropertiesViewModel("bym", "BYM", "#5E4BB0", @"C:\p\bym\project.md")
        {
            Holdout = new HoldoutLocation(@"C:\mine", false) { Source = HoldoutSource.Chosen },
            HoldoutByConvention = own,
            ChosenHoldout = @"C:\mine",
        };

        Assert.That((properties.HoldoutValue, properties.CanResetHoldout, properties.HoldoutChanged), Is.EqualTo((@"C:\mine (not there)", true, false)));
        Assert.That(properties.HoldoutExplanation, Does.StartWith("You chose it. The folder is not there"));
        Assert.That(properties.ChooseHoldout(@"C:\"), Does.StartWith("A whole drive"));
        Assert.That(properties.ChooseHoldout("relative"), Does.StartWith("Choose a full path"));

        properties.ResetHoldout();

        Assert.That((properties.HoldoutValue, properties.HoldoutChoice, properties.HoldoutChanged), Is.EqualTo((own.Folder, (string?)null, true)));
        Assert.That(properties.HoldoutExplanation, Does.StartWith("Found by convention once you press OK: Theseus's own, made on first use."));

        Assert.That(properties.ChooseHoldout(@"C:\mine"), Is.Null);
        Assert.That(properties.HoldoutChanged, Is.False, "chosen back to what it was");
    }

    [Test]
    public void ProjectProperties_ARecordedRepositoryNotCloned_SaysSoOnce()
    {
        var properties = new ProjectPropertiesViewModel("theseus", "Theseus", "#5E4BB0", "project.md")
        {
            Holdout = new HoldoutLocation(Path.Combine(_root, "Theseus-holdout"), false) { Source = HoldoutSource.Recorded },
        };

        Assert.That(properties.HoldoutValue, Is.EqualTo($"{Path.Combine(_root, "Theseus-holdout")} (not there)"));
        Assert.That(properties.HoldoutExplanation, Does.StartWith("project.md records a holdout repository, but no clone of it is there"));
        Assert.That(properties.HoldoutExplanation, Does.Not.Contain("Recorded in project.md"), "said once, not twice");
    }

    private static Workstream Card(string key, string? project) =>
        new Workstream(key, key, "#208DD7", false, @"C:\work\" + key, [], "Carry on.", 0, "New", false, false, false, null, [], null,
            DateTimeOffset.UnixEpoch) with { Project = project };
}
