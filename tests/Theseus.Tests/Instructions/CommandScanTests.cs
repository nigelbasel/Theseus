using Theseus.Core.Instructions;

namespace Theseus.Tests.Instructions;

[TestFixture]
public class CommandScanTests
{
    private string _root = string.Empty;
    private string _claude = string.Empty;
    private string _repo = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-commands-{Guid.NewGuid():N}");
        _claude = Path.Combine(_root, "home", ".claude");
        _repo = Path.Combine(_root, "work", "repo");
        Directory.CreateDirectory(_claude);
        Directory.CreateDirectory(Path.Combine(_repo, ".git"));
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void For_RunsThePersonalSkillWhenTheProjectHasOneOfTheSameName()
    {
        var personal = Skill(Path.Combine(_claude, "skills"), "deploy");
        var project = Skill(Path.Combine(_repo, ".claude", "skills"), "deploy");

        var entries = Scan();

        Assert.That(Entry(entries, personal).HiddenBy, Is.Null);
        Assert.That(Entry(entries, project).HiddenBy, Is.EqualTo(personal));
    }

    [Test]
    public void For_PrefersASkillToACommandOfTheSameName()
    {
        var skill = Skill(Path.Combine(_repo, ".claude", "skills"), "deploy");
        var command = Write(Path.Combine(_claude, "commands", "deploy.md"), "---\ndescription: Old way\n---\n");

        var entries = Scan();

        Assert.That(Entry(entries, command).HiddenBy, Is.EqualTo(skill));
        Assert.That(Entry(entries, command).Description, Is.EqualTo("Old way"));
    }

    [Test]
    public void For_NamesAPersonalSkillAfterItsDirectoryNotItsFrontMatter()
    {
        var skill = Skill(Path.Combine(_claude, "skills"), "deploy", frontMatterName: "fancy");

        Assert.That(Entry(Scan(), skill).Name, Is.EqualTo("deploy"));
    }

    [Test]
    public void For_PrefixesPluginSkillsSoTheyNeverTakeAnothersName()
    {
        var plugin = Path.Combine(_root, "plugins", "tools");
        var pluginSkill = Skill(Path.Combine(plugin, "skills"), "review", frontMatterName: "fancy");
        var personal = Skill(Path.Combine(_claude, "skills"), "fancy");

        var entries = CommandScan.For(_repo, [], _claude, [new InstalledPlugin("tools", plugin)]);

        Assert.That(Entry(entries, pluginSkill).Name, Is.EqualTo("tools:fancy"));
        Assert.That(Entry(entries, pluginSkill).HiddenBy, Is.Null);
        Assert.That(Entry(entries, personal).HiddenBy, Is.Null);
    }

    [Test]
    public void For_LetsAProjectAgentTakeTheNameFromAPersonalOne()
    {
        var personal = Write(Path.Combine(_claude, "agents", "reviewer.md"), "---\nname: reviewer\n---\n");
        var project = Write(Path.Combine(_repo, ".claude", "agents", "strict.md"), "---\nname: reviewer\n---\n");

        var entries = Scan();

        Assert.That(Entry(entries, personal).HiddenBy, Is.EqualTo(project));
        Assert.That(Entry(entries, project).HiddenBy, Is.Null);
    }

    [Test]
    public void For_FindsProjectSkillsUpToTheRepositoryRootAndNoFurther()
    {
        var atRoot = Skill(Path.Combine(_repo, ".claude", "skills"), "release");
        var above = Skill(Path.Combine(_root, "work", ".claude", "skills"), "elsewhere");
        var below = Path.Combine(_repo, "src");
        Directory.CreateDirectory(below);

        var paths = CommandScan.For(below, [], _claude, []).Select(entry => entry.Path).ToList();

        Assert.That(paths, Does.Contain(atRoot));
        Assert.That(paths, Does.Not.Contain(above));
    }

    [Test]
    public void For_QualifiesASkillBelowTheWorkingDirectoryByWhereItIs()
    {
        var nested = Skill(Path.Combine(_repo, "apps", "web", ".claude", "skills"), "deploy");
        var root = Skill(Path.Combine(_repo, ".claude", "skills"), "deploy");

        var entries = Scan();

        Assert.That(Entry(entries, nested).Name, Is.EqualTo("apps/web:deploy"));
        Assert.That(Entry(entries, nested).LoadsOnDemand, Is.True);
        Assert.That(Entry(entries, nested).HiddenBy, Is.Null);
        Assert.That(Entry(entries, root).HiddenBy, Is.Null);
    }

    [Test]
    public void For_IncludesSkillsFromADirectoryAddedToTheSession()
    {
        var added = Path.Combine(_root, "shared");
        var skill = Skill(Path.Combine(added, ".claude", "skills"), "lint");

        Assert.That(Entry(CommandScan.For(_repo, [added], _claude, []), skill).Origin,
            Is.EqualTo(CommandOrigin.AdditionalDirectory));
    }

    private IReadOnlyList<CommandEntry> Scan() => CommandScan.For(_repo, [], _claude, []);

    private static CommandEntry Entry(IReadOnlyList<CommandEntry> entries, string path) =>
        entries.Single(entry => string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));

    private static string Skill(string skillsDirectory, string directory, string? frontMatterName = null) =>
        Write(Path.Combine(skillsDirectory, directory, "SKILL.md"),
            $"---\nname: {frontMatterName ?? directory}\ndescription: Does {directory}\n---\n# {directory}\n");

    private static string Write(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);

        return Path.GetFullPath(path);
    }
}
