using System.Text.Json;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

[TestFixture]
public class HookInstallationTests
{
    [Test]
    public void IsInstalled_FindsAHookWhateverEventItIsRegisteredOn()
    {
        const string settings = """
            {"hooks":{"Stop":[{"hooks":[{"type":"command","command":"C:/x/Theseus.Hook.exe"}]}]}}
            """;

        Assert.That(HookInstallation.IsInstalled(settings), Is.True);
    }

    [Test]
    public void IsInstalled_StillFindsItAfterTheInstallationHasMoved()
    {
        const string settings = """
            {"hooks":{"Stop":[{"hooks":[{"type":"command","command":"D:/elsewhere/theseus.hook.exe"}]}]}}
            """;

        Assert.That(HookInstallation.IsInstalled(settings), Is.True,
            "matched on the executable's name, so moving the installation does not read as uninstalled");
    }

    [Test]
    public void IsInstalled_IsFalseWhenSomebodyElsesHooksAreConfigured()
    {
        const string settings = """
            {"hooks":{"PreToolUse":[{"hooks":[{"type":"command","command":"prettier --write"}]}]}}
            """;

        Assert.That(HookInstallation.IsInstalled(settings), Is.False);
    }

    [Test]
    public void IsInstalled_IsFalseWhenThereAreNoHooksAtAll()
    {
        Assert.That(HookInstallation.IsInstalled("""{"theme":"dark"}"""), Is.False);
        Assert.That(HookInstallation.IsInstalled(string.Empty), Is.False);
        Assert.That(HookInstallation.IsInstalled(null), Is.False);
    }

    [Test]
    public void IsInstalled_SaysNotInstalledRatherThanThrowingOnSettingsItCannotRead()
    {
        Assert.That(HookInstallation.IsInstalled("{ half a file"), Is.False);
    }

    [Test]
    public void Snippet_ProducesConfigurationThatParsesAndRegistersEveryEvent()
    {
        var snippet = HookInstallation.Snippet(@"C:\Program Files\Theseus\Theseus.Hook.exe");

        using var document = JsonDocument.Parse(snippet);
        var hooks = document.RootElement.GetProperty("hooks");

        foreach (var name in HookInstallation.Events)
        {
            Assert.That(hooks.TryGetProperty(name, out _), Is.True, $"{name} should be registered");
        }
    }

    [Test]
    public void Snippet_IsRecognisedByTheCheckThatLooksForIt()
    {
        var snippet = HookInstallation.Snippet(@"C:\Program Files\Theseus\Theseus.Hook.exe");

        Assert.That(HookInstallation.IsInstalled(snippet), Is.True,
            "what is handed out and what is looked for must be the same thing");
    }

    [Test]
    public void Snippet_EscapesAWindowsPathSoItSurvivesBeingReadBack()
    {
        var path = Path.Combine("C:", "Program Files", "Theseus", "Theseus.Hook.exe");

        using var document = JsonDocument.Parse(HookInstallation.Snippet(path));

        var command = document.RootElement
            .GetProperty("hooks").GetProperty("Stop")[0]
            .GetProperty("hooks")[0]
            .GetProperty("command").GetString();

        Assert.That(command, Is.EqualTo($"\"{path}\""),
            "the command is run through a shell, where an unquoted Program Files is two words");
    }

}
