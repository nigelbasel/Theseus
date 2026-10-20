using Theseus.Core.Launching;

namespace Theseus.Tests.Launching;

[TestFixture]
public class StartNoticeTests
{
    [Test]
    public void For_SaysNothingWasStartedWhenNothingWasAndNothingWasRefused()
    {
        Assert.That(StartNotice.For([], []), Is.EqualTo("Nothing was started."));
    }

    [Test]
    public void For_NamesTheOneWorkstreamThatStarted()
    {
        Assert.That(StartNotice.For(["Theseus"], []), Is.EqualTo("Started Theseus."));
    }

    [Test]
    public void For_CountsSessionsWhenSeveralStarted()
    {
        Assert.That(StartNotice.For(["Theseus", "Tailhawk"], []), Is.EqualTo("Started 2 sessions."));
    }

    [Test]
    public void For_GivesTheReasonWhenTheTerminalRefusedEveryStart()
    {
        var notice = StartNotice.For([], [StartNotice.Refusal("Theseus", "Access is denied.")]);

        Assert.That(notice, Is.EqualTo(
            "Theseus did not start: the terminal would not start (Access is denied.). The log has the details."));
    }

    [Test]
    public void For_SaysWhatStartedAndWhatDidNotWhenOnlySomeWereRefused()
    {
        var notice = StartNotice.For(["Tailhawk"], [StartNotice.Refusal("Theseus", "Access is denied.")]);

        Assert.That(notice, Does.StartWith("Started Tailhawk. "));
        Assert.That(notice, Does.Contain("Theseus did not start"));
    }
}
