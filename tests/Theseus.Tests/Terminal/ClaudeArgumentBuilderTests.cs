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
            Spec(addDirectories: [@"C:\work\repo\Core", @"C:\work\repo\Harbor"])).ToList();

        Assert.That(argv.IndexOf("Pick up where we left off."),
            Is.LessThan(argv.IndexOf("--add-dir")));
    }

    [Test]
    public void Build_PlacesEveryExtraDirectoryAfterASingleAddDirFlag()
    {
        var argv = ClaudeArgumentBuilder.Build(
            Spec(addDirectories: [@"C:\work\repo\Core", @"C:\work\repo\Harbor"])).ToList();

        Assert.That(argv.Count(a => a == "--add-dir"), Is.EqualTo(1));
        Assert.That(argv[^2], Is.EqualTo(@"C:\work\repo\Core"));
        Assert.That(argv[^1], Is.EqualTo(@"C:\work\repo\Harbor"));
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
        Assert.That(argv, Has.Count.EqualTo(6));
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

    [TestCase(LaunchMode.FirstRun)]
    [TestCase(LaunchMode.Resume)]
    public void Build_StartsEverySessionInAutoMode_SoItRunsUnattended(LaunchMode mode)
    {
        var argv = ClaudeArgumentBuilder.Build(Spec(mode)).ToList();

        Assert.That(argv, Does.Contain("--permission-mode"));
        Assert.That(argv[argv.IndexOf("--permission-mode") + 1], Is.EqualTo("auto"));
    }

    [Test]
    public void Build_PutsThePermissionModeBeforeTheVariadicFlags()
    {
        var argv = ClaudeArgumentBuilder.Build(Spec(addDirectories: [@"C:\work\Core"])).ToList();

        Assert.That(argv.IndexOf("--permission-mode"), Is.GreaterThan(-1).And.LessThan(argv.IndexOf("--add-dir")));
    }

    [Test]
    public void AHeadlessRun_SpeaksStreamJsonBothWays_AndKeepsItsPromptOffTheCommandLine()
    {
        var argv = ClaudeArgumentBuilder.Build(Spec() with { Headless = true }).ToList();

        Assert.That(argv.Take(6), Is.EqualTo(new[] { "-p", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose" }));
        Assert.That(argv, Does.Not.Contain("Pick up where we left off."));
        Assert.That(argv, Does.Contain("--session-id").And.Contain("auto"));
    }

    [Test]
    public void AHeadlessRun_CarriesClaudeCodesOwnCostCap()
    {
        var argv = ClaudeArgumentBuilder.Build(Spec() with { Headless = true, MaxBudgetUsd = 2.5m }).ToList();

        Assert.That(argv[argv.IndexOf("--max-budget-usd") + 1], Is.EqualTo("2.50"));
    }

    [Test]
    public void ATab_NeverGetsTheHeadlessFlags()
    {
        var argv = ClaudeArgumentBuilder.Build(Spec() with { MaxBudgetUsd = 2.5m }).ToList();

        Assert.That(argv, Does.Not.Contain("-p").And.Not.Contain("--max-budget-usd"));
    }
}
