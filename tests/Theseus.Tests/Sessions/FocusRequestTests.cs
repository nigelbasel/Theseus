using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class FocusRequestTests
{
    [Test]
    public void SessionIdIn_ReadsTheSessionTheTaskbarAskedFor()
    {
        Assert.That(FocusRequest.SessionIdIn(["--focus", "abc-123"]), Is.EqualTo("abc-123"));
    }

    [Test]
    public void SessionIdIn_IgnoresACommandLineThatAsksForTheWindow()
    {
        Assert.That(FocusRequest.SessionIdIn([]), Is.Null);
    }

    [Test]
    public void SessionIdIn_IgnoresTheSwitchWhenNothingFollowsIt()
    {
        Assert.That(FocusRequest.SessionIdIn(["--focus"]), Is.Null);
        Assert.That(FocusRequest.SessionIdIn(["--focus", "  "]), Is.Null);
    }

    [Test]
    public void SessionIdIn_FindsTheSwitchAfterOtherArguments()
    {
        Assert.That(FocusRequest.SessionIdIn(["--quiet", "--focus", "abc-123"]), Is.EqualTo("abc-123"));
    }

    [Test]
    public void ArgumentsFor_QuotesTheSessionSoTheShellCannotSplitIt()
    {
        Assert.That(FocusRequest.ArgumentsFor("abc-123"), Is.EqualTo("--focus \"abc-123\""));
    }

    [Test]
    public void ArgumentsFor_AndSessionIdIn_AgreeOnWhatWasAsked()
    {
        var arguments = FocusRequest.ArgumentsFor("abc-123").Replace("\"", string.Empty).Split(' ');

        Assert.That(FocusRequest.SessionIdIn(arguments), Is.EqualTo("abc-123"));
    }
}
