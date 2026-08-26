using Theseus.Core.Launching;
using Theseus.Core.Terminal;

namespace Theseus.Tests.Terminal;

[TestFixture]
public class ClaudeArgumentBuilderTests
{
    private const string SessionId = "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa";

    private static LaunchSpec Spec(
        LaunchMode mode = LaunchMode.FirstRun,
        string name = "Contact form validation",
        string prompt = "Pick up where we left off.",
        IReadOnlyList<string>? addDirectories = null) =>
        new(SessionId, mode, @"C:\Users\dev\AppData\Roaming\npm\claude.cmd", @"C:\work\repo",
            name, prompt, addDirectories ?? []);

    [Test]
    public void Build_MintsTheIdentityOnAFirstRun()
    {
        var argv = ClaudeArgumentBuilder.Build(Spec()).ToList();

        Assert.That(argv[0], Is.EqualTo("--session-id"));
        Assert.That(argv[1], Is.EqualTo(SessionId));
    }

    [Test]
    public void Build_ContinuesTheSameIdentityOnAResume()
    {
        var argv = ClaudeArgumentBuilder.Build(Spec(LaunchMode.Resume)).ToList();

        Assert.That(argv[0], Is.EqualTo("--resume"));
        Assert.That(argv[1], Is.EqualTo(SessionId));
    }

    [Test]
    public void Build_NamesTheSession()
    {
        var argv = ClaudeArgumentBuilder.Build(Spec()).ToList();

        Assert.That(argv[argv.IndexOf("-n") + 1], Is.EqualTo("Contact form validation"));
    }

    [Test]
    public void Build_PutsThePromptBeforeAddDirBecauseAddDirIsVariadic()
    {
        var argv = ClaudeArgumentBuilder.Build(
            Spec(addDirectories: [@"C:\work\repo\Core", @"C:\work\repo\Nexus"])).ToList();

        Assert.That(argv.IndexOf("Pick up where we left off."),
            Is.LessThan(argv.IndexOf("--add-dir")));
    }

    [Test]
    public void Build_PlacesEveryExtraDirectoryAfterASingleAddDirFlag()
    {
        var argv = ClaudeArgumentBuilder.Build(
            Spec(addDirectories: [@"C:\work\repo\Core", @"C:\work\repo\Nexus"])).ToList();

        Assert.That(argv.Count(a => a == "--add-dir"), Is.EqualTo(1));
        Assert.That(argv[^2], Is.EqualTo(@"C:\work\repo\Core"));
        Assert.That(argv[^1], Is.EqualTo(@"C:\work\repo\Nexus"));
    }

    [Test]
    public void Build_OmitsAddDirEntirelyWhenThereAreNoExtraDirectories()
    {
        var argv = ClaudeArgumentBuilder.Build(Spec()).ToList();

        Assert.That(argv, Does.Not.Contain("--add-dir"));
    }

    [Test]
    public void Build_OmitsThePromptWhenThereIsNoneRatherThanPassingAnEmptyArgument()
    {
        var argv = ClaudeArgumentBuilder.Build(Spec(prompt: "   ")).ToList();

        Assert.That(argv, Has.No.Member(string.Empty));
        Assert.That(argv, Has.Count.EqualTo(4));
    }

    [Test]
    public void Build_OmitsTheNameWhenThereIsNone()
    {
        var argv = ClaudeArgumentBuilder.Build(Spec(name: "")).ToList();

        Assert.That(argv, Does.Not.Contain("-n"));
    }

    [Test]
    public void Build_LeavesValuesUnescapedBecauseNoneOfThemReachTheTerminalCommandLine()
    {
        var argv = ClaudeArgumentBuilder.Build(Spec(name: "A;B", prompt: "run x; then y")).ToList();

        Assert.That(argv, Does.Contain("A;B"));
        Assert.That(argv, Does.Contain("run x; then y"));
    }
}
