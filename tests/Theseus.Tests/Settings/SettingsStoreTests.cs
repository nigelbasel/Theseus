using Theseus.Core.Settings;

namespace Theseus.Tests.Settings;

[TestFixture]
public class SettingsStoreTests
{
    private string _directory = string.Empty;
    private string _path = string.Empty;
    private SettingsStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "settings.json");
        _store = new SettingsStore(_path);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void Read_GivesTheDefaultsBeforeAnythingIsEverSaved()
    {
        Assert.That(_store.Read(), Is.EqualTo(TheseusSettings.Default));
    }

    [Test]
    public void Write_RoundTripsEveryField()
    {
        var settings = new TheseusSettings(@"C:\work\plans", "my-window", @"C:\wt.exe",
            @"C:\claude.exe", "https://example.atlassian.net", "Read the notes first.",
            "Carry on from the handoff.", "Write a handoff and stop.",
            SessionsPanelCollapsed: true, Lanes: ["Now", "Later"], Theme: AppTheme.Dark)
        {
            ShowArchived = true,
            Areas = ["Research", "Dev"],
            CollapsedStages = ["Paused"],
            CollapsedCards = ["theseus", "tailhawk"],
            CollapsedProjects = ["bym"],
            ShowActiveOnly = true,
            ColumnsBy = BoardColumnsBy.SessionState,
            RowsBy = BoardRowsBy.Project,
            ViewFolds = new Dictionary<string, IReadOnlyList<string>> { ["rows:Project"] = ["Theseus"] },
            ProjectsFolder = @"C:\projects",
            WorktreesFolder = @"C:\work",
            ClonesFolder = @"C:\src",
            GuardNoticeFor = "trunk guard:NotInstalled:ABC",
            AutoModeSourceControlHosts = ["git.example.com"],
            AutoModeInternalDomains = ["example.com"],
            AutoModeProductionPatterns = ["prod"],
            ProjectCardPlaces = new Dictionary<string, ProjectCardPlace> { ["a-project"] = new("In progress", "Dev") },
            PaneSizes = new Dictionary<string, double> { ["ProjectView:Pane"] = 420 },
            ProjectColours = new Dictionary<string, string> { ["a-project"] = "#208DD7" },
            ProjectGrouping = new Dictionary<string, ProjectViewGrouping> { ["a-project"] = ProjectViewGrouping.Objectives },
            ProjectRunStates = new Dictionary<string, IReadOnlyList<string>> { ["a-project"] = ["git-22", "git-23"] },
            ProjectLeads = new Dictionary<string, string> { ["a-project"] = "git-22" },
            CardsStoppedByOwner = ["git-24"],
            ProjectBranchRules = new Dictionary<string, ProjectBranchGrant>
            {
                ["a-project"] = new(new DateTimeOffset(2026, 11, 21, 20, 0, 0, TimeSpan.Zero), "nigel/a-project"),
                ["bug-fixing"] = new(new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.Zero), "nigel/bug-fixing") { Kind = BranchGrantKind.ItemBranches },
            },
            CardsToldBranchRules = ["git-25"],
            UnattendedProjects = ["bug-fixing"],
            HiddenProjectFolders = ["notes"],
            ProjectHoldoutFolders = new Dictionary<string, string> { ["bym"] = @"C:\mine" },
            ProjectUpdaterRuns = new Dictionary<string, string> { ["bym"] = "Does not read: line 3" },
            ProjectTags = new Dictionary<string, IReadOnlyList<string>> { ["bym"] = ["bym", "security"] },
            TagColours = new Dictionary<string, string> { ["bym"] = "#208DD7" },
            Budgets = new Theseus.Core.Budgets.BudgetSettings(RunCostLimitUsd: 2.5m, RunTokenLimit: 9_000),
            ProjectBudgets = new Dictionary<string, Theseus.Core.Budgets.BudgetSettings> { ["bym"] = new(CardDayCostLimitUsd: 3m) },
            Breakers = new Theseus.Core.Breakers.BreakerSettings(Crashes: 5),
            MaxHeadlessRuns = 7,
            ProjectRunModes = new Dictionary<string, Theseus.Core.Running.RunMode> { ["bym"] = Theseus.Core.Running.RunMode.Headless },
            Duties = new DutySettings { DailyRunCap = 12, QuietHoursOn = true, QueueProjects = ["bym"] },
            ChainsFilters = new Theseus.Core.Links.ChainFilters(ActiveOnly: true, WaitingOnMe: false, HideCompleted: false),
            ChainsFolds = new Dictionary<string, bool> { ["git-22"] = false, ["project:Theseus"] = true },
        };

        _store.Write(settings);

        var read = _store.Read();

        Assert.That((read.ProjectsFolder, read.WorktreesFolder), Is.EqualTo((@"C:\projects", @"C:\work")));
        Assert.That(read.ClonesFolder, Is.EqualTo(@"C:\src"));
        Assert.That(read.ProjectCardPlaces["a-project"], Is.EqualTo(new ProjectCardPlace("In progress", "Dev")));
        Assert.That(read.Lanes, Is.EqualTo(settings.Lanes));
        Assert.That(read.Areas, Is.EqualTo(settings.Areas));
        Assert.That(read.CollapsedStages, Is.EqualTo(settings.CollapsedStages));
        Assert.That(read.CollapsedCards, Is.EqualTo(settings.CollapsedCards));
        Assert.That(read.CollapsedProjects, Is.EqualTo(settings.CollapsedProjects));
        Assert.That(read.ShowActiveOnly, Is.True);
        Assert.That(read.ViewFolds["rows:Project"], Is.EqualTo(new[] { "Theseus" }));
        Assert.That(read.PaneSizes["ProjectView:Pane"], Is.EqualTo(420));
        Assert.That(read.ProjectColours["a-project"], Is.EqualTo("#208DD7"));
        Assert.That(read.ProjectGrouping["a-project"], Is.EqualTo(ProjectViewGrouping.Objectives));
        Assert.That(read.ProjectBudgets["bym"].CardDayCostLimitUsd, Is.EqualTo(3m));
        Assert.That(read.ProjectRunModes["bym"], Is.EqualTo(Theseus.Core.Running.RunMode.Headless));
        Assert.That(read.ProjectRunStates["a-project"], Is.EqualTo(new[] { "git-22", "git-23" }));
        Assert.That(read.ProjectLeads["a-project"], Is.EqualTo("git-22"));
        Assert.That(read.CardsStoppedByOwner, Is.EqualTo(new[] { "git-24" }));
        Assert.That(read.ProjectBranchRules["a-project"],
            Is.EqualTo(new ProjectBranchGrant(new DateTimeOffset(2026, 11, 21, 20, 0, 0, TimeSpan.Zero), "nigel/a-project")));
        Assert.That(read.ProjectBranchRules["bug-fixing"].Kind, Is.EqualTo(BranchGrantKind.ItemBranches));
        Assert.That(read.CardsToldBranchRules, Is.EqualTo(new[] { "git-25" }));
        Assert.That(read.UnattendedProjects, Is.EqualTo(new[] { "bug-fixing" }));
        Assert.That(read.HiddenProjectFolders, Is.EqualTo(new[] { "notes" }));
        Assert.That(read.ProjectHoldoutFolders, Is.EqualTo(new Dictionary<string, string> { ["bym"] = @"C:\mine" }));
        Assert.That(read.ProjectUpdaterRuns, Is.EqualTo(new Dictionary<string, string> { ["bym"] = "Does not read: line 3" }));
        Assert.That(read.ProjectTags["bym"], Is.EqualTo(new[] { "bym", "security" }));
        Assert.That(read.TagColours, Is.EqualTo(new Dictionary<string, string> { ["bym"] = "#208DD7" }));
        Assert.That((read.Duties.DailyRunCap, read.Duties.QuietHoursOn), Is.EqualTo((12, true)));
        Assert.That(read.Duties.QueueProjects, Is.EqualTo(new[] { "bym" }));
        Assert.That(read.ChainsFilters, Is.EqualTo(settings.ChainsFilters));
        Assert.That(read.ChainsFolds, Is.EqualTo(settings.ChainsFolds));
        Assert.That((read.AutoModeSourceControlHosts, read.AutoModeInternalDomains, read.AutoModeProductionPatterns),
            Is.EqualTo((settings.AutoModeSourceControlHosts, settings.AutoModeInternalDomains, settings.AutoModeProductionPatterns)));
        Assert.That(read with
            {
                Lanes = settings.Lanes, Areas = settings.Areas, CollapsedAreas = settings.CollapsedAreas,
                CollapsedStages = settings.CollapsedStages, CollapsedCards = settings.CollapsedCards, CollapsedProjects = settings.CollapsedProjects,
                ViewFolds = settings.ViewFolds, ColumnWeights = settings.ColumnWeights, RowHeights = settings.RowHeights, AreaWindows = settings.AreaWindows,
                ProjectCardPlaces = settings.ProjectCardPlaces, PaneSizes = settings.PaneSizes, ProjectColours = settings.ProjectColours,
                ProjectGrouping = settings.ProjectGrouping, ProjectRunStates = settings.ProjectRunStates, ProjectLeads = settings.ProjectLeads,
                CardsStoppedByOwner = settings.CardsStoppedByOwner, ProjectBranchRules = settings.ProjectBranchRules,
                CardsToldBranchRules = settings.CardsToldBranchRules, HiddenProjectFolders = settings.HiddenProjectFolders,
                UnattendedProjects = settings.UnattendedProjects,
                ProjectHoldoutFolders = settings.ProjectHoldoutFolders,
                ProjectUpdaterRuns = settings.ProjectUpdaterRuns,
                ProjectTags = settings.ProjectTags, TagColours = settings.TagColours,
                ProjectBudgets = settings.ProjectBudgets, ProjectRunModes = settings.ProjectRunModes,
                Duties = read.Duties with { QueueProjects = settings.Duties.QueueProjects },
                ChainsFolds = settings.ChainsFolds,
                AutoModeSourceControlHosts = settings.AutoModeSourceControlHosts, AutoModeInternalDomains = settings.AutoModeInternalDomains,
                AutoModeProductionPatterns = settings.AutoModeProductionPatterns,
            }, Is.EqualTo(settings),
            "a record holding a list compares it by reference, so the lists are checked separately");
    }

    [Test]
    public void Read_AcceptsAFileWrittenByAnOlderBuildThatHadFewerFields()
    {
        File.WriteAllText(_path, """
            {
              "planDirectory": "C:\\work\\plans",
              "terminalWindowName": "theseus",
              "terminalExecutablePath": "",
              "claudeExecutablePath": "",
              "theme": "Dark"
            }
            """);

        var settings = _store.Read();

        Assert.That(settings.PlanDirectory, Is.EqualTo(@"C:\work\plans"));
        Assert.That(settings.Theme, Is.EqualTo(AppTheme.Dark));
        Assert.That(settings.JiraBaseUrl, Is.Empty);
        Assert.That(settings.SessionPreamble, Is.Empty);
        Assert.That(settings.ChainsFilters, Is.EqualTo(new Theseus.Core.Links.ChainFilters()), "Hide completed on, the rest off");
        Assert.That(settings.ChainsFolds, Is.Empty);
    }

    [Test]
    public void Read_LeavesThePanelOpenWhenTheSettingPredatesIt()
    {
        File.WriteAllText(_path, """
            {"planDirectory":"C:\\work\\plans","terminalWindowName":"theseus","theme":"System"}
            """);

        Assert.That(_store.Read().SessionsPanelCollapsed, Is.False);
    }

    [Test]
    public void Write_RemembersThatThePanelWasFoldedAway()
    {
        _store.Write(TheseusSettings.Default with { SessionsPanelCollapsed = true });

        Assert.That(_store.Read().SessionsPanelCollapsed, Is.True);
    }

    [Test]
    public void Read_FillsAMissingPlanDirectoryWithTheDefaultRatherThanLeavingItBlank()
    {
        File.WriteAllText(_path, """{"planDirectory":"  ","terminalWindowName":"  ","theme":"System"}""");

        var settings = _store.Read();

        Assert.That(settings.PlanDirectory, Is.EqualTo(TheseusSettings.DefaultPlanDirectory));
        Assert.That(settings.TerminalWindowName, Is.EqualTo(TheseusSettings.Default.TerminalWindowName));
    }

    [Test]
    public void Read_FallsBackToTheDefaultsRatherThanCrashingOnACorruptedFile()
    {
        File.WriteAllText(_path, "{ this is not settings");

        Assert.That(_store.Read(), Is.EqualTo(TheseusSettings.Default));
    }

    [Test]
    public void Read_KeepsAnEmptyPreambleDistinctFromAnAbsentOne()
    {
        _store.Write(TheseusSettings.Default with { SessionPreamble = "   " });

        Assert.That(_store.Read().SessionPreamble, Is.EqualTo("   "));
    }

    [Test]
    public void Write_LeavesNoTemporaryFileBehind()
    {
        _store.Write(TheseusSettings.Default);

        Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
    }

    [Test]
    public void Write_StoresTheThemeByNameSoTheFileSurvivesReorderingTheEnum()
    {
        _store.Write(TheseusSettings.Default with { Theme = AppTheme.Light });

        Assert.That(File.ReadAllText(_path), Does.Contain("Light"));
    }

    [Test]
    public void Read_ShowsTheWorkingBoardWhenTheSettingPredatesIt()
    {
        Assert.That(TheseusSettings.Default.ShowArchived, Is.False,
            "a board that opened onto what had been put away would look empty for no reason");
    }

    [Test]
    public void Write_RoundTripsTheWindowPositionThroughDisk()
    {
        var placement = new Theseus.Core.Layout.WindowPlacement(
            new Theseus.Core.Layout.LayoutRect(120, 80, 1600, 1000), IsMaximised: true);

        _store.Write(TheseusSettings.Default with { MainWindow = placement });

        Assert.That(_store.Read().MainWindow, Is.EqualTo(placement),
            "a nested record has to survive the file or the window opens somewhere arbitrary");
    }

    [Test]
    public void Read_HasNoWindowPositionBeforeOneIsSaved()
    {
        Assert.That(TheseusSettings.Default.MainWindow, Is.Null,
            "a first run has nothing to restore and must fall back to fitting the screen");
    }
}
