using Theseus.Launcher;

namespace Theseus.Tests.Launcher;

[TestFixture]
public class LauncherOptionsTests
{
    [Test]
    public void Parse_ReadsThePipeAndTicket()
    {
        var options = LauncherOptions.Parse(["--pipe", "Theseus.Launch", "--ticket", "a1b2c3"]);

        Assert.That(options!.PipeName, Is.EqualTo("Theseus.Launch"));
        Assert.That(options.Ticket, Is.EqualTo("a1b2c3"));
    }

    [Test]
    public void Parse_AcceptsTheArgumentsInEitherOrder()
    {
        var options = LauncherOptions.Parse(["--ticket", "a1b2c3", "--pipe", "Theseus.Launch"]);

        Assert.That(options!.PipeName, Is.EqualTo("Theseus.Launch"));
        Assert.That(options.Ticket, Is.EqualTo("a1b2c3"));
    }

    [Test]
    public void Parse_RejectsAMissingTicket()
    {
        Assert.That(LauncherOptions.Parse(["--pipe", "Theseus.Launch"]), Is.Null);
    }

    [Test]
    public void Parse_RejectsAMissingPipe()
    {
        Assert.That(LauncherOptions.Parse(["--ticket", "a1b2c3"]), Is.Null);
    }

    [Test]
    public void Parse_RejectsAnUnknownArgumentRatherThanIgnoringIt()
    {
        Assert.That(LauncherOptions.Parse(["--pipe", "p", "--ticket", "t", "--prompt", "rm -rf"]), Is.Null);
    }

    [Test]
    public void Parse_RejectsAnEmptyCommandLine()
    {
        Assert.That(LauncherOptions.Parse([]), Is.Null);
    }
}
