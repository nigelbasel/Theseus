using Theseus.Core.Terminal;

namespace Theseus.Tests.Terminal;

[TestFixture]
public class WtCommandBuilderTests
{
    private static TerminalTabRequest Request(string title = "GDPR MCP contact forms",
        string workingDirectory = @"C:\dev\tfsgit") =>
        new("nigel-orchestrator", title, "#208DD7", workingDirectory,
            @"C:\dev\git\Theseus\Theseus.Launcher.exe",
            ["--pipe", "Theseus.Launch", "--ticket", "a1b2c3"]);

    [Test]
    public void Build_TargetsTheNamedWindowBeforeTheSubcommand()
    {
        var argv = WtCommandBuilder.Build(Request()).ToList();

        Assert.That(argv[0], Is.EqualTo("-w"));
        Assert.That(argv[1], Is.EqualTo("nigel-orchestrator"));
        Assert.That(argv[2], Is.EqualTo("new-tab"));
    }

    [Test]
    public void Build_CarriesTitleColourAndDirectory()
    {
        var argv = WtCommandBuilder.Build(Request()).ToList();

        Assert.That(argv, Does.Contain("--title"));
        Assert.That(argv[argv.IndexOf("--title") + 1], Is.EqualTo("GDPR MCP contact forms"));
        Assert.That(argv[argv.IndexOf("--tabColor") + 1], Is.EqualTo("#208DD7"));
        Assert.That(argv[argv.IndexOf("-d") + 1], Is.EqualTo(@"C:\dev\tfsgit"));
    }

    [Test]
    public void Build_PutsTheTargetAfterASeparator()
    {
        var argv = WtCommandBuilder.Build(Request()).ToList();
        var separator = argv.IndexOf("--");

        Assert.That(separator, Is.GreaterThan(0));
        Assert.That(argv[separator + 1], Is.EqualTo(@"C:\dev\git\Theseus\Theseus.Launcher.exe"));
        Assert.That(argv[separator + 2], Is.EqualTo("--pipe"));
        Assert.That(argv.Count, Is.EqualTo(separator + 6));
    }

    [Test]
    public void Build_EscapesSemicolonsInATitleSoTheyDoNotStartANewTab()
    {
        var argv = WtCommandBuilder.Build(Request(title: "A;B")).ToList();

        Assert.That(argv[argv.IndexOf("--title") + 1], Is.EqualTo(@"A\;B"));
    }

    [Test]
    public void Build_EscapesSemicolonsInAWorkingDirectory()
    {
        var argv = WtCommandBuilder.Build(Request(workingDirectory: @"C:\odd;path")).ToList();

        Assert.That(argv[argv.IndexOf("-d") + 1], Is.EqualTo(@"C:\odd\;path"));
    }

    [Test]
    public void Build_LeavesOrdinaryTextUntouched()
    {
        var argv = WtCommandBuilder.Build(Request(title: "Legacy job migration")).ToList();

        Assert.That(argv[argv.IndexOf("--title") + 1], Is.EqualTo("Legacy job migration"));
    }

    [Test]
    public void Escape_HandlesSeveralSemicolons()
    {
        Assert.That(WtArgumentEscaper.Escape("a;b;c"), Is.EqualTo(@"a\;b\;c"));
    }

    [Test]
    public void Escape_ReturnsEmptyForEmpty()
    {
        Assert.That(WtArgumentEscaper.Escape(string.Empty), Is.EqualTo(string.Empty));
    }
    [Test]
    public void Build_KeepsTheTitleTheApplicationWasGivenSoTheTabCanBeFoundAgain()
    {
        var argv = WtCommandBuilder.Build(Request()).ToList();

        Assert.That(argv, Does.Contain("--suppressApplicationTitle"));
        Assert.That(argv.IndexOf("--suppressApplicationTitle"), Is.LessThan(argv.IndexOf("--")));
    }
}
