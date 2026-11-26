using System.Text.Json.Nodes;
using Theseus.Core.Guards;

namespace Theseus.Tests.Guards;

/// <summary>
/// Auto mode's environment, built by Theseus from what the owner enters once and written to Claude Code's settings only at
/// his click, after the exact change is shown (guards-ship-with-theseus brief step 3, Nigel 2026-11-26). The repository
/// holds the template alone: an organisation's hosts and domains live in the owner's own settings.
/// </summary>
[TestFixture]
public class AutoModeEnvironmentTests
{
    private static readonly AutoModeEntries Entries = new(
        ["git.example.com", "github.com/someone"],
        ["git.example.com"],
        ["prod", "live"]);

    private string _config = string.Empty;

    [SetUp]
    public void MakeFixtureFolder()
    {
        _config = Path.Combine(Path.GetTempPath(), "theseus-automode-tests", Guid.NewGuid().ToString("N"), ".claude");
        Directory.CreateDirectory(_config);
    }

    [Test]
    public void TheOwnersEntries_AreWhereTheTemplateNamesThem()
    {
        var lines = AutoModeEnvironment.Build(Entries);

        Assert.That(lines, Has.Some.StartsWith("**Source control**: Trusted remotes are on git.example.com and github.com/someone."));
        Assert.That(lines, Has.Member("**Trusted internal domains**: git.example.com. No other domain is trusted by name."));
        Assert.That(lines, Has.Some.StartsWith("**Sensitive remote targets**:").And.Contains("`prod` or `live`"));
    }

    [Test]
    public void WithNothingEntered_ThoseLinesSayNoneConfigured()
    {
        var lines = AutoModeEnvironment.Build(new AutoModeEntries([], [], []));

        Assert.That(lines, Has.Member("**Source control**: None configured"));
        Assert.That(lines, Has.Member("**Trusted internal domains**: None configured"));
        Assert.That(lines, Has.None.Contains("{"));
    }

    [Test]
    public void TheTemplate_NamesTheTrunkRules_AndNoOrganisation()
    {
        var text = string.Join('\n', AutoModeEnvironment.Build(new AutoModeEntries([], [], [])));

        Assert.That(text, Does.Contain("main, master and trunk are protected"));
        Assert.That(text, Does.Not.Contain("nurtur").IgnoreCase.And.Not.Contain("briefyourmarket").IgnoreCase.And.Not.Contain("nigel").IgnoreCase,
            "the repository is public: what is the owner's own is entered in Options");
    }

    [Test]
    public void TheChange_IsShownInFull_AndWritesNothing()
    {
        File.WriteAllText(SettingsPath, """{ "autoMode": { "environment": [ "an old line" ] } }""");

        var change = Environment().Change(Entries);

        Assert.That(change, Does.Contain("an old line").And.Contain("git.example.com and github.com/someone"));
        Assert.That(File.ReadAllText(SettingsPath), Does.Not.Contain("git.example.com"));
    }

    [Test]
    public void Writing_ReplacesTheEnvironmentAlone_KeepingTheRestAndTheOldFile()
    {
        File.WriteAllText(SettingsPath, """{ "model": "opus", "autoMode": { "allow": [ "x" ], "environment": [ "an old line" ] } }""");

        var outcome = Environment().Write(Entries);

        Assert.That(outcome.Succeeded, Is.True, outcome.Message);
        var settings = JsonNode.Parse(File.ReadAllText(SettingsPath))!;
        Assert.That(settings["model"]!.GetValue<string>(), Is.EqualTo("opus"));
        Assert.That(settings["autoMode"]!["allow"]!.AsArray(), Has.Count.EqualTo(1));
        Assert.That(settings["autoMode"]!["environment"]!.AsArray().Select(line => line!.GetValue<string>()),
            Is.EqualTo(AutoModeEnvironment.Build(Entries)));
        Assert.That(File.ReadAllText(SettingsPath + ".previous"), Does.Contain("an old line"));
        Assert.That(Environment().Matches(Entries), Is.True);
    }

    [Test]
    public void WithNoSettingsFile_ItIsWritten()
    {
        Assert.That(Environment().Matches(Entries), Is.False);

        Assert.That(Environment().Write(Entries).Succeeded, Is.True);
        Assert.That(Environment().Matches(Entries), Is.True);
    }

    [Test]
    public void WhenTheSettingsMatch_ThereIsNoChangeToShow()
    {
        Environment().Write(Entries);

        Assert.That(Environment().Change(Entries), Is.Null);
    }

    [Test]
    public void SettingsOfAnUnexpectedShape_AreNeverReplaced()
    {
        const string odd = """{ "autoMode": [ "not an object" ] }""";
        File.WriteAllText(SettingsPath, odd);

        Assert.That(Environment().Change(Entries), Is.Null);
        Assert.That(Environment().Write(Entries).Succeeded, Is.False);
        Assert.That(File.ReadAllText(SettingsPath), Is.EqualTo(odd));
    }

    [Test]
    public void TheSettingsStayReadable()
    {
        Environment().Write(Entries);

        Assert.That(File.ReadAllText(SettingsPath), Does.Contain("`prod`").And.Not.Contain(@"\u00"));
    }

    [TearDown]
    public void RemoveFixtureFolder()
    {
        var root = Path.GetDirectoryName(_config)!;

        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private string SettingsPath => Path.Combine(_config, "settings.json");

    private AutoModeEnvironment Environment() => new(_config);
}
