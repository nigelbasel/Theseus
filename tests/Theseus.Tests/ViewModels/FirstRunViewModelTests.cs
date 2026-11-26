using Theseus.App.ViewModels;
using Theseus.Core.Links;
using Theseus.Core.Settings;
using Theseus.Core.Setup;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The first-run setup's steps as the owner walks them: Back, Next, Skip and Finish, what is remembered, the hook written only
/// with consent, and New project opening the folder step when there is no projects folder (first-run-setup, Nigel 2026-11-25).
/// </summary>
[TestFixture]
public class FirstRunViewModelTests
{
    private string _config = string.Empty;
    private MemorySettingsStore _store = null!;
    private IReadOnlyList<ToolFinding> _tools = [];
    private int _changes;

    [SetUp]
    public void SetUp()
    {
        _config = Path.Combine(Path.GetTempPath(), "theseus-first-run-vm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_config);
        _store = new MemorySettingsStore();
        _tools = [new ToolFinding("Claude Code", true, "Claude Code 2.1.300")];
        _changes = 0;
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_config, recursive: true);

    private string SettingsFile => Path.Combine(_config, "settings.json");

    private FirstRunViewModel Setup() =>
        new(_store, new StopHookRegistration(_config, @"C:\Theseus\Theseus.Hook.exe"),
            [new GuardViewModel(new Theseus.Core.Guards.GuardInstallation(Theseus.Core.Guards.GuardCatalog.Holdout, _config, _config))], () => _tools, () => _changes++);

    [Test]
    public void Opening_ShowsOnlyWhatIsMissing_FromTheFirstStep()
    {
        _tools = [new ToolFinding("Claude Code", true, "ok")];
        var setup = Setup();

        setup.Open();

        Assert.That(setup.IsOpen, Is.True);
        Assert.That(setup.Steps, Is.EqualTo(new[] { SetupStep.Folders, SetupStep.Hook, SetupStep.Guards }));
        Assert.That((setup.Current, setup.StepHeading), Is.EqualTo((SetupStep.Folders, "Step 1 of 3: Folders")));
        Assert.That(setup.IsFoldersStep, Is.True);
    }

    [Test]
    public void NextAndBack_MoveThroughTheSteps_AndTheLastOffersFinish()
    {
        var setup = Setup();
        setup.Open();

        Assert.That(setup.BackCommand.CanExecute(null), Is.False, "nothing before the first step");

        setup.NextCommand.Execute(null);
        Assert.That((setup.Current, setup.IsLast), Is.EqualTo((SetupStep.Hook, false)));

        setup.BackCommand.Execute(null);
        Assert.That(setup.Current, Is.EqualTo(SetupStep.Folders));

        setup.NextCommand.Execute(null);
        setup.NextCommand.Execute(null);
        Assert.That((setup.Current, setup.IsLast), Is.EqualTo((SetupStep.Guards, true)));
        Assert.That(setup.NextCommand.CanExecute(null), Is.False, "Finish takes Next's place");
    }

    [Test]
    public void Finish_ClosesIt_AndIsRemembered()
    {
        var setup = Setup();
        setup.Open();

        setup.FinishCommand.Execute(null);

        Assert.That((setup.IsOpen, _store.Settings.FirstRunSetupDone), Is.EqualTo((false, true)));
        Assert.That(FirstRunSetup.ShowsAtStartup(_store.Settings), Is.False);
    }

    [Test]
    public void Skip_ClosesIt_FromAnyStep_AndIsRemembered()
    {
        var setup = Setup();
        setup.Open();
        setup.NextCommand.Execute(null);

        setup.SkipCommand.Execute(null);

        Assert.That((setup.IsOpen, _store.Settings.FirstRunSetupDone), Is.EqualTo((false, true)));
        Assert.That(_store.Settings.ProjectsFolder, Is.Empty, "skipping chooses nothing");
    }

    [Test]
    public void ClosingTheWindow_CountsAsSkip()
    {
        var setup = Setup();
        setup.Open();

        setup.Closed();

        Assert.That((setup.IsOpen, _store.Settings.FirstRunSetupDone), Is.EqualTo((false, true)));
    }

    [Test]
    public void ChoosingTheFolders_SavesThem_AndTellsTheBoard()
    {
        var setup = Setup();
        setup.Open();

        setup.ProjectsFolder = @"C:\projects ";
        setup.WorktreesFolder = @"C:\work";
        setup.ClonesFolder = @"C:\code";

        Assert.That((_store.Settings.ProjectsFolder, _store.Settings.WorktreesFolder, _store.Settings.ClonesFolder),
            Is.EqualTo((@"C:\projects", @"C:\work", @"C:\code")));
        Assert.That(_changes, Is.EqualTo(3));
        Assert.That(setup.ProjectsFolderValue, Is.EqualTo(@"C:\projects"));
    }

    [Test]
    public void TheHookStep_ShowsTheExactEntry_AndWritesNothingWithoutConsent()
    {
        var setup = Setup();
        string? shown = null;
        setup.ConfirmChange = change =>
        {
            shown = change;

            return false;
        };
        setup.Open();

        setup.AddHookCommand.Execute(null);

        Assert.That(shown, Does.Contain("Theseus.Hook.exe").And.Contain(SettingsFile));
        Assert.That(setup.HookEntry, Does.Contain("Theseus.Hook.exe"), "the entry is on the step itself, before anything is asked");
        Assert.That(File.Exists(SettingsFile), Is.False, "never written without consent");
        Assert.That((setup.HookIsRegistered, setup.HookResult), Is.EqualTo((false, "Nothing was changed.")));
    }

    [Test]
    public void TheHookStep_WithConsent_RegistersTheHook()
    {
        var setup = Setup();
        setup.ConfirmChange = _ => true;
        setup.Open();

        setup.AddHookCommand.Execute(null);

        Assert.That(setup.HookIsRegistered, Is.True, setup.HookResult);
        Assert.That(File.ReadAllText(SettingsFile), Does.Contain("Theseus.Hook.exe"));
    }

    [Test]
    public void TheHookStep_WithNobodyToAsk_WritesNothing()
    {
        var setup = Setup();
        setup.Open();

        setup.AddHookCommand.Execute(null);

        Assert.That(File.Exists(SettingsFile), Is.False);
    }

    [Test]
    public void NewProject_WithNoProjectsFolder_OpensTheFolderStep()
    {
        _store.Settings = TheseusSettings.Default with { FirstRunSetupDone = true };
        var setup = Setup();

        Assert.That(setup.OpenForNewProject(), Is.True);
        Assert.That((setup.IsOpen, setup.Current), Is.EqualTo((true, SetupStep.Folders)));
    }

    [Test]
    public void NewProject_WithAProjectsFolder_IsLeftToGoAhead()
    {
        _store.Settings = TheseusSettings.Default with { ProjectsFolder = @"C:\projects" };
        var setup = Setup();

        Assert.That((setup.OpenForNewProject(), setup.IsOpen), Is.EqualTo((false, false)));
    }

    [Test]
    public void OpenedFromTheMenu_WithEverythingSetUp_SaysSo_AndOffersOnlyFinish()
    {
        _store.Settings = TheseusSettings.Default with { ProjectsFolder = @"C:\projects" };
        File.WriteAllText(SettingsFile, """{"hooks":{"Stop":[{"hooks":[{"type":"command","command":"C:/x/Theseus.Hook.exe"}]}]}}""");
        var setup = Setup();
        setup.IsGuardReady = () => true;

        setup.Open();

        Assert.That(setup.Steps, Is.Empty);
        Assert.That((setup.IsAllSet, setup.IsLast, setup.StepHeading), Is.EqualTo((true, true, "Everything is set up")));
    }
}
