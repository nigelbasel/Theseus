using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Sessions;

namespace Theseus.Tests.Launching;

/// <summary>
/// Spotting a workstream whose tab is still open with nothing running in it.
///
/// The real case, 2026-09-07: sessions left in their tabs over a weekend, their Claude processes
/// gone by Monday, and Start resumed three of them into second tabs beside the first. Nothing was
/// registered for them, so every check Theseus had said go — but the tab was there to be seen.
/// </summary>
[TestFixture]
public class AbandonedTabsTests
{
    private const string SessionId = "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa";

    [Test]
    public void Among_FindsAWorkstreamWhoseTabOutlivedItsSession()
    {
        var stopping = Workstream("gdpr-mcp", "MCP: GDPR Changes");

        Assert.That(
            AbandonedTabs.Among([stopping], ["MCP: GDPR Changes", "TailHawk"], SessionRegistrySnapshot.Empty)
                .Select(w => w.Key),
            Is.EqualTo(new[] { "gdpr-mcp" }));
    }

    [Test]
    public void Among_IgnoresAWorkstreamWithNoTabOfItsOwn()
    {
        Assert.That(
            AbandonedTabs.Among([Workstream("dev", "Job queue")], ["TailHawk"], SessionRegistrySnapshot.Empty),
            Is.Empty);
    }

    [Test]
    public void Among_IgnoresAWorkstreamWhoseSessionIsActuallyRunning()
    {
        var running = Workstream("tailhawk", "TailHawk") with { LastSessionId = SessionId };

        Assert.That(
            AbandonedTabs.Among([running], ["TailHawk"], Live(SessionId)), Is.Empty,
            "a live session's tab is doing its job; a Start already refuses that workstream");
    }

    [Test]
    public void Among_MatchesTheNameLooselyBecauseATitleIsTypedByAPerson()
    {
        var workstream = Workstream("gdpr", "MCP: GDPR Changes");

        Assert.That(
            AbandonedTabs.Among([workstream], ["mcp gdpr changes"], SessionRegistrySnapshot.Empty),
            Has.Count.EqualTo(1),
            "punctuation and case must not decide whether a second tab is opened");
    }

    [Test]
    public void Among_FindsNothingWhenNoTabCouldBeRead()
    {
        // Every terminal elevated, or none open. The guard is best-effort and says so by finding
        // nothing rather than by guessing.
        Assert.That(
            AbandonedTabs.Among([Workstream("gdpr", "MCP: GDPR Changes")], [], SessionRegistrySnapshot.Empty),
            Is.Empty);
    }

    [Test]
    public void Among_KeepsTheOrderItWasGiven()
    {
        var first = Workstream("one", "First");
        var second = Workstream("two", "Second");

        Assert.That(
            AbandonedTabs.Among([first, second], ["Second", "First"], SessionRegistrySnapshot.Empty)
                .Select(w => w.Key),
            Is.EqualTo(new[] { "one", "two" }));
    }

    [Test]
    public void Among_RefusesNothingRatherThanThrowing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(() => AbandonedTabs.Among(null!, [], SessionRegistrySnapshot.Empty), Throws.ArgumentNullException);
            Assert.That(() => AbandonedTabs.Among([], null!, SessionRegistrySnapshot.Empty), Throws.ArgumentNullException);
            Assert.That(() => AbandonedTabs.Among([], [], null!), Throws.ArgumentNullException);
        });
    }

    private static SessionRegistrySnapshot Live(string sessionId) =>
        new([new LiveSession(4242, sessionId, @"C:\work\repo", "name", "busy", "cli", DateTimeOffset.UtcNow)], 0, 0);

    private static Workstream Workstream(string key, string title) =>
        new(key, title, "#208DD7", ColorIsManual: true, @"C:\work\repo", [], "Carry on.", 1,
            "New", IsBlocked: false, ReopenTomorrow: true, IsArchived: false, LastSessionId: null,
            [], null, DateTimeOffset.UtcNow);
}
