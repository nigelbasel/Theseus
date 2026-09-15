using Theseus.Core.Launching;
using Theseus.Core.Settings;

namespace Theseus.Tests.Settings;

[TestFixture]
public class DefaultStartModeTests
{
    private string _directory = string.Empty;
    private string _path = string.Empty;
    private SettingsStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-start-mode-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "settings.json");
        _store = new SettingsStore(_path);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void Default_KeepsResumingSoNoButtonChangesWhatItDoesUntilAskedTo()
    {
        Assert.That(TheseusSettings.Default.DefaultStartMode, Is.EqualTo(StartMode.Resume));
    }

    [Test]
    public void Write_RemembersAChoiceToStartFresh()
    {
        _store.Write(TheseusSettings.Default with { DefaultStartMode = StartMode.Fresh });

        Assert.That(_store.Read().DefaultStartMode, Is.EqualTo(StartMode.Fresh));
    }

    [Test]
    public void Write_StoresTheChoiceByNameSoTheFileCanBeReadAndEdited()
    {
        _store.Write(TheseusSettings.Default with { DefaultStartMode = StartMode.Fresh });

        Assert.That(File.ReadAllText(_path), Does.Contain("\"defaultStartMode\": \"Fresh\""));
    }

    [Test]
    public void Read_ResumesOnAFileWrittenBeforeThereWasAChoice()
    {
        File.WriteAllText(_path, """{ "planDirectory": "C:\\work\\plans", "terminalWindowName": "theseus" }""");

        Assert.That(_store.Read().DefaultStartMode, Is.EqualTo(StartMode.Resume));
    }
}
