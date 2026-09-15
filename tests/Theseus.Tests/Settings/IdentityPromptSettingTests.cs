using Theseus.Core.Settings;

namespace Theseus.Tests.Settings;

[TestFixture]
public class IdentityPromptSettingTests
{
    private string _directory = string.Empty;
    private string _path = string.Empty;
    private SettingsStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-identity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "settings.json");
        _store = new SettingsStore(_path);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void Write_RemembersWordingOfYourOwn()
    {
        _store.Write(TheseusSettings.Default with { IdentityPrompt = "You are {title}." });

        Assert.That(_store.Read().IdentityPrompt, Is.EqualTo("You are {title}."));
    }

    [Test]
    public void Read_LeavesItEmptyOnAFileWrittenBeforeItCouldBeChanged()
    {
        File.WriteAllText(_path, """{ "planDirectory": "C:\\work\\plans", "terminalWindowName": "theseus" }""");

        Assert.That(_store.Read().IdentityPrompt, Is.Empty);
    }

    [Test]
    public void Read_TreatsAnExplicitNullAsEmpty()
    {
        File.WriteAllText(_path, """{ "planDirectory": "C:\\work\\plans", "identityPrompt": null }""");

        Assert.That(_store.Read().IdentityPrompt, Is.Empty);
    }
}
