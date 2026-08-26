using Theseus.Core.Domain;
using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class LostSessionsTests
{
    [Test]
    public void Find_SpotsASessionRestartedOutFromUnderItsWorkstream()
    {
        var workstreams = new[] { Workstream("investigate-nexus-mcp-server", "Investigate Nexus MCP Server", "old-id") };
        var sessions = new[] { Session("new-id", "Investigate Nexus MCP Server") };

        var lost = LostSessions.Find(workstreams, sessions).Single();

        Assert.That(lost.WorkstreamKey, Is.EqualTo("investigate-nexus-mcp-server"));
        Assert.That(lost.SessionId, Is.EqualTo("new-id"));
    }

    [Test]
    public void Find_MatchesTheNameLooselyBecauseAKeyIsNotATitle()
    {
        var workstreams = new[] { Workstream("nexus", "Investigate Nexus MCP Server", "old-id") };
        var sessions = new[] { Session("new-id", "investigate-nexus-mcp-server") };

        Assert.That(LostSessions.Find(workstreams, sessions), Has.Count.EqualTo(1));
    }

    [Test]
    public void Find_SaysNothingAboutAWorkstreamAlreadyFollowingItsSession()
    {
        var workstreams = new[] { Workstream("theseus", "Theseus", "live-id") };
        var sessions = new[] { Session("live-id", "Theseus") };

        Assert.That(LostSessions.Find(workstreams, sessions), Is.Empty);
    }

    [Test]
    public void Find_LeavesAloneAWorkstreamThatIsRunningSomethingElse()
    {
        var workstreams = new[] { Workstream("theseus", "Theseus", "live-id") };
        var sessions = new[] { Session("live-id", "Theseus"), Session("other-id", "Theseus") };

        Assert.That(LostSessions.Find(workstreams, sessions), Is.Empty,
            "a workstream with a session running is not one the board has lost");
    }

    [Test]
    public void Find_RefusesToGuessWhenTwoWorkstreamsShareAName()
    {
        var workstreams = new[]
        {
            Workstream("first", "Nexus work", null),
            Workstream("second", "nexus-work", null),
        };

        Assert.That(LostSessions.Find(workstreams, [Session("new-id", "Nexus work")]), Is.Empty,
            "binding a workstream to the wrong session is worse than leaving it plainly unbound");
    }

    [Test]
    public void Find_IgnoresASessionMatchingNoWorkstream()
    {
        var workstreams = new[] { Workstream("theseus", "Theseus", null) };

        Assert.That(LostSessions.Find(workstreams, [Session("new-id", "Something else")]), Is.Empty);
    }

    [Test]
    public void Find_IgnoresASessionWithNoNameToMatchOn()
    {
        var workstreams = new[] { Workstream("theseus", "Theseus", null) };

        Assert.That(LostSessions.Find(workstreams, [Session("new-id", null)]), Is.Empty);
    }

    [Test]
    public void Find_IgnoresAnArchivedWorkstream()
    {
        var workstream = Workstream("theseus", "Theseus", null) with { IsArchived = true };

        Assert.That(LostSessions.Find([workstream], [Session("new-id", "Theseus")]), Is.Empty);
    }

    [Test]
    public void Find_IgnoresASessionThatIsNotAnInteractiveOne()
    {
        var workstreams = new[] { Workstream("theseus", "Theseus", null) };
        var scripted = Session("new-id", "Theseus") with { Entrypoint = "sdk" };

        Assert.That(LostSessions.Find(workstreams, [scripted]), Is.Empty,
            "a scripted run is not the workstream's tab come back");
    }

    private static Workstream Workstream(string key, string title, string? lastSessionId) =>
        new(key, title, "#208DD7", ColorIsManual: true, @"C:\work", [], "prompt", 1, "Today",
            IsBlocked: false, ReopenTomorrow: true, IsArchived: false, lastSessionId, [], null,
            DateTimeOffset.UtcNow);

    private static LiveSession Session(string id, string? name) =>
        new(1234, id, @"C:\work", name, "busy", "cli", DateTimeOffset.UtcNow);
}
