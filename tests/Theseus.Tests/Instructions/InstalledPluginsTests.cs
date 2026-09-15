using Theseus.Core.Instructions;

namespace Theseus.Tests.Instructions;

[TestFixture]
public class InstalledPluginsTests
{
    private string _claude = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _claude = Path.Combine(Path.GetTempPath(), $"theseus-plugins-{Guid.NewGuid():N}", ".claude");
        Directory.CreateDirectory(Path.Combine(_claude, "plugins"));
    }

    [TearDown]
    public void TearDown() => Directory.Delete(Path.GetDirectoryName(_claude)!, recursive: true);

    [Test]
    public void Read_GivesOnlyThePluginsThatAreSwitchedOn()
    {
        File.WriteAllText(Path.Combine(_claude, "settings.json"),
            """{ "enabledPlugins": { "posthog@official": true, "old@official": false } }""");
        File.WriteAllText(Path.Combine(_claude, "plugins", "installed_plugins.json"), """
            { "version": 2, "plugins": {
                "posthog@official": [ { "installPath": "C:\\cache\\posthog\\1.1.63" } ],
                "old@official": [ { "installPath": "C:\\cache\\old\\0.1" } ] } }
            """);

        var plugins = InstalledPlugins.Read(_claude);

        Assert.That(plugins, Is.EqualTo(new[] { new InstalledPlugin("posthog", @"C:\cache\posthog\1.1.63") }));
    }

    [Test]
    public void Read_GivesNothingWhenNothingIsInstalled()
    {
        Assert.That(InstalledPlugins.Read(_claude), Is.Empty);
    }

    [Test]
    public void Read_GivesNothingRatherThanFailingOnABrokenFile()
    {
        File.WriteAllText(Path.Combine(_claude, "settings.json"), "{ not json");
        File.WriteAllText(Path.Combine(_claude, "plugins", "installed_plugins.json"), "{}");

        Assert.That(InstalledPlugins.Read(_claude), Is.Empty);
    }
}
