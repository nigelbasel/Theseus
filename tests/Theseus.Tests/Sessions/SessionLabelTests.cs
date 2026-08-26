using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class SessionLabelTests
{
    [Test]
    public void For_UsesTheNameTheSessionWasGiven()
    {
        Assert.That(SessionLabel.For(Session(name: "contact-forms")), Is.EqualTo("contact-forms"));
    }

    [Test]
    public void For_FallsBackToTheFolderTheSessionIsWorkingIn()
    {
        Assert.That(SessionLabel.For(Session(cwd: @"C:\dev\tfsgit\Automation.UI")), Is.EqualTo("Automation.UI"));
    }

    [Test]
    public void For_IgnoresATrailingSeparatorWhenNamingTheFolder()
    {
        Assert.That(SessionLabel.For(Session(cwd: @"C:\dev\tfsgit\Automation.UI\")), Is.EqualTo("Automation.UI"));
    }

    [Test]
    public void For_FallsBackToTheIdentifierWhenThereIsNothingElseToGoOn()
    {
        Assert.That(SessionLabel.For(Session()), Is.EqualTo("6d6942b6"));
    }

    [Test]
    public void For_ShortensANameTooLongForAMenu()
    {
        var label = SessionLabel.For(Session(name: new string('x', 60)));

        Assert.That(label, Has.Length.EqualTo(40));
        Assert.That(label, Does.EndWith("…"));
    }

    [Test]
    public void For_LeavesANameThatFitsAlone()
    {
        var name = new string('x', 40);

        Assert.That(SessionLabel.For(Session(name: name)), Is.EqualTo(name));
    }

    private static LiveSession Session(string? name = null, string? cwd = null) =>
        new(1234, "6d6942b6-162a-4da4-a420-b265967ed98a", cwd, name, "idle", "cli", DateTimeOffset.UtcNow);
}
