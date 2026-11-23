using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

/// <summary>The running, idle or waiting mark beside a one-click Focus (project-view-usability item 8).</summary>
[TestFixture]
public class SessionMarkTests
{
    [TestCase(SessionLifecycle.Launching, "● running")]
    [TestCase(SessionLifecycle.Running, "● running")]
    [TestCase(SessionLifecycle.Idle, "○ idle")]
    [TestCase(SessionLifecycle.NeedsAttention, "◆ waiting")]
    [TestCase(SessionLifecycle.NotStarted, "")]
    [TestCase(SessionLifecycle.Ended, "")]
    [TestCase(SessionLifecycle.Ghost, "")]
    public void Of_SaysRunningIdleOrWaiting_AndNothingWithNoSession(SessionLifecycle lifecycle, string mark)
    {
        Assert.That(SessionMark.Of(lifecycle), Is.EqualTo(mark));
    }
}
