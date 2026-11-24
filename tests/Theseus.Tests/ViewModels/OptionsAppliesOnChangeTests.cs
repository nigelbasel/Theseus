using Theseus.App.ViewModels;
using Theseus.Core.Launching;
using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Options applies every change as it is made, and has no Save (W6, checklist G138: a settings page has no Save button).
/// Each row writes the settings file the moment its value changes, so closing Options keeps what was changed.
/// </summary>
[TestFixture]
public class OptionsAppliesOnChangeTests
{
    private static IEnumerable<TestCaseData> Rows()
    {
        yield return Row("plan directory", panel => panel.PlanDirectory = @"C:\plans", settings => settings.PlanDirectory, @"C:\plans");
        yield return Row("terminal window name", panel => panel.TerminalWindowName = "desk", settings => settings.TerminalWindowName, "desk");
        yield return Row("Windows Terminal", panel => panel.TerminalExecutablePath = @"C:\wt.exe", settings => settings.TerminalExecutablePath, @"C:\wt.exe");
        yield return Row("Claude Code", panel => panel.ClaudeExecutablePath = @"C:\claude.exe", settings => settings.ClaudeExecutablePath, @"C:\claude.exe");
        yield return Row("Jira", panel => panel.JiraBaseUrl = "https://example.atlassian.net", settings => settings.JiraBaseUrl, "https://example.atlassian.net");
        yield return Row("holdout remote", panel => panel.HoldoutRemote = " host/owner/repo ", settings => settings.HoldoutRemote, "host/owner/repo");
        yield return Row("projects folder", panel => panel.ProjectsFolder = @"  C:\projects  ", settings => settings.ProjectsFolder, @"C:\projects");
        yield return Row("worktrees folder", panel => panel.WorktreesFolder = @"C:\work ", settings => settings.WorktreesFolder, @"C:\work");
        yield return Row("pre-releases", panel => panel.IncludePreReleases = true, settings => settings.IncludePreReleases, true);
        yield return Row("standing prompt", panel => panel.SessionPreamble = "Be brief.", settings => settings.SessionPreamble, "Be brief.");
        yield return Row("continue prompt", panel => panel.ContinuePrompt = "Carry on.", settings => settings.ContinuePrompt, "Carry on.");
        yield return Row("stop prompt", panel => panel.StopPrompt = "Stop now.", settings => settings.StopPrompt, "Stop now.");
        yield return Row("identity prompt", panel => panel.IdentityPrompt = "You are {key}. Notes: {handoff}", settings => settings.IdentityPrompt, "You are {key}. Notes: {handoff}");
        yield return Row("start mode", panel => panel.FreshByDefault = true, settings => settings.DefaultStartMode, StartMode.Fresh);
        yield return Row("stages", panel => panel.LaneList = "Doing\nDone", settings => string.Join("|", settings.Lanes), "Doing|Done");
        yield return Row("areas", panel => panel.AreaList = "dev\nops", settings => string.Join("|", settings.Areas), "dev|ops");
    }

    private static TestCaseData Row<T>(string name, Action<SettingsViewModel> change, Func<TheseusSettings, T> read, T expected) =>
        new TestCaseData(change, (Func<TheseusSettings, object?>)(settings => read(settings)), expected).SetName($"EachRow_WritesThrough_AtOnce({name})");

    [TestCaseSource(nameof(Rows))]
    public void EachRow_WritesThrough_AtOnce(Action<SettingsViewModel> change, Func<TheseusSettings, object?> read, object expected)
    {
        MemorySettingsStore store = new();
        SettingsViewModel panel = new(store, () => { });
        panel.OpenCommand.Execute(null);

        change(panel);

        Assert.That(read(store.Settings), Is.EqualTo(expected), "written the moment it changed, with nothing to press");
    }

    [Test]
    public void ShowAgain_WritesThrough_AtOnce()
    {
        MemorySettingsStore store = new() { Settings = TheseusSettings.Default with { HiddenProjectFolders = ["leadpro-mcp", "notes"] } };
        SettingsViewModel panel = new(store, () => { });
        panel.OpenCommand.Execute(null);

        panel.ShowProjectFolderCommand.Execute("notes");

        Assert.That(store.Settings.HiddenProjectFolders, Is.EqualTo(new[] { "leadpro-mcp" }));
    }

    [Test]
    public void TheBuiltInIdentityWording_IsStoredAsEmpty_SoALaterBuiltInStillReachesSessions()
    {
        MemorySettingsStore store = new() { Settings = TheseusSettings.Default with { IdentityPrompt = "Mine {handoff}" } };
        SettingsViewModel panel = new(store, () => { });
        panel.OpenCommand.Execute(null);

        panel.ResetIdentityPromptCommand.Execute(null);

        Assert.That(store.Settings.IdentityPrompt, Is.Empty);
    }

    [Test]
    public void ARow_KeepsWhatWasWrittenElsewhere_SinceOptionsOpened()
    {
        MemorySettingsStore store = new();
        SettingsViewModel panel = new(store, () => { });
        panel.OpenCommand.Execute(null);
        store.Settings = store.Settings with { HiddenProjectFolders = ["notes"], ShowArchived = true };

        panel.PlanDirectory = @"C:\plans";

        Assert.That((store.Settings.HiddenProjectFolders, store.Settings.ShowArchived), Is.EqualTo((new[] { "notes" }, true)));
    }

    [Test]
    public void Opening_WritesNothing()
    {
        MemorySettingsStore store = new();
        SettingsViewModel panel = new(store, () => { });

        panel.OpenCommand.Execute(null);

        Assert.That(store.Writes, Is.Zero);
    }

    [Test]
    public void ThereIsNoSaveCommand()
    {
        Assert.That(typeof(SettingsViewModel).GetProperty("SaveCommand"), Is.Null, "a settings page has no Save (G138)");
    }

    [Test]
    public void Closing_AfterAChange_TellsTheBoard_Once()
    {
        MemorySettingsStore store = new();
        int told = 0;
        SettingsViewModel panel = new(store, () => told++);
        panel.OpenCommand.Execute(null);
        panel.LaneList = "Doing\nDone";
        panel.AreaList = "dev\nops";

        panel.CloseCommand.Execute(null);

        Assert.That((panel.IsOpen, told), Is.EqualTo((false, 1)), "the board reads the new stages and areas once Options closes");
    }

    [Test]
    public void Closing_WithNoChange_LeavesTheBoardAlone()
    {
        MemorySettingsStore store = new();
        int told = 0;
        SettingsViewModel panel = new(store, () => told++);
        panel.OpenCommand.Execute(null);

        panel.IsOpen = false;

        Assert.That(told, Is.Zero);
    }

    [Test]
    public void ProjectProperties_ClosesOptionsFirst_SoOnlyOneDialogIsOpen()
    {
        MemorySettingsStore store = new();
        bool openWhenAsked = true;
        SettingsViewModel panel = new(store, () => { })
        {
            HasProjectCard = _ => true,
        };
        panel.OpenProjectHoldouts = _ => openWhenAsked = panel.IsOpen;
        panel.OpenCommand.Execute(null);
        panel.HoldoutEntries = [new("theseus", @"C:\h", Theseus.Core.Links.HoldoutSource.Recorded)];

        panel.OpenProjectPropertiesCommand.Execute(panel.HoldoutRows[0]);

        Assert.That(openWhenAsked, Is.False, "Options closes before Project properties opens (W8, G17)");
    }
}
