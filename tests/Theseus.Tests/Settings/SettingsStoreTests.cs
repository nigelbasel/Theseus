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
            SessionsPanelCollapsed: true, Theme: AppTheme.Dark);

        _store.Write(settings);

        Assert.That(_store.Read(), Is.EqualTo(settings));
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
}
