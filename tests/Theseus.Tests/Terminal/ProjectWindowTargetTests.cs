using Theseus.Core.Domain;
using Theseus.Core.Terminal;

namespace Theseus.Tests.Terminal;

/// <summary>
/// Bringing a project's window forward works through a live session whose run was launched into it (project-view
/// AC 21); with none it says why, for the disabled command's tooltip.
/// </summary>
[TestFixture]
public class ProjectWindowTargetTests
{
    private const string Window = "theseus-project-p";

    private static SessionRun Run(string session, string? window, int hour) =>
        new(Guid.NewGuid(), session, "a-card", @"C:\work", null, SessionLaunchMode.FirstRun, "Carry on.", "A card", "#208DD7",
            new DateTimeOffset(2026, 11, 20, hour, 0, 0, TimeSpan.Zero), null, null, null, null, SessionExitReason.Unknown)
        {
            WindowName = window,
        };

    [Test]
    public void ALiveSessionLaunchedIntoTheProjectsWindow_IsTheOneUsed()
    {
        var target = ProjectWindowTarget.For("p", Window,
            [("other", "s2", true), ("a-card", "s1", true)],
            [Run("s1", Window, 9), Run("s2", "theseus-dev", 9)]);

        Assert.That(target.SessionId, Is.EqualTo("s1"));
    }

    [Test]
    public void WithNoLiveSession_ThereIsNoneAndTheReasonSaysSo()
    {
        var target = ProjectWindowTarget.For("p", Window, [("a-card", "s1", false)], [Run("s1", Window, 9)]);

        Assert.That(target.SessionId, Is.Null);
        Assert.That(target.Reason, Is.EqualTo($"No session of p is running, so its window {Window} has nothing to bring forward"));
    }

    [Test]
    public void ALiveSessionLaunchedElsewhere_IsNotUsed_AndTheReasonSaysWhy()
    {
        var target = ProjectWindowTarget.For("p", Window, [("a-card", "s1", true)], [Run("s1", "theseus-dev", 9)]);

        Assert.That(target.SessionId, Is.Null);
        Assert.That(target.Reason, Does.StartWith($"None of p's running sessions was launched into {Window}"));
    }

    [Test]
    public void ASessionsLatestRun_Decides()
    {
        var resumedElsewhere = ProjectWindowTarget.For("p", Window, [("a-card", "s1", true)],
            [Run("s1", Window, 9), Run("s1", "theseus-dev", 10)]);
        var resumedHere = ProjectWindowTarget.For("p", Window, [("a-card", "s1", true)],
            [Run("s1", "theseus-dev", 9), Run("s1", Window, 10)]);

        Assert.That((resumedElsewhere.SessionId, resumedHere.SessionId), Is.EqualTo(((string?)null, "s1")));
    }

    [Test]
    public void ARunFromBeforeWindowsWereRecorded_DoesNotCount()
    {
        Assert.That(ProjectWindowTarget.For("p", Window, [("a-card", "s1", true)], [Run("s1", null, 9)]).SessionId, Is.Null);
    }
}
