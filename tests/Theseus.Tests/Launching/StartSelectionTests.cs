using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Sessions;

namespace Theseus.Tests.Launching;

[TestFixture]
public class StartSelectionTests
{
    private static readonly string[] Columns =
        ["Today", "In flight", "Parked", "Done"];

    private const string SessionId = "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa";

    [Test]
    public void ForBoard_NeverStartsTheLastLaneBecauseThatIsWhereFinishedWorkLives()
    {
        var workstreams = new[]
        {
            Workstream("contact-forms", "Today"),
            Workstream("job-queue", "In flight"),
            Workstream("nexus", "Parked"),
            Workstream("finished", "Done"),
        };

        var selected = StartSelection.ForBoard(workstreams, Columns, SessionRegistrySnapshot.Empty);

        Assert.That(selected.Select(w => w.Key),
            Is.EqualTo(new[] { "contact-forms", "job-queue", "nexus" }));
    }

    [Test]
    public void ForBoard_StartsInLaneOrderThenBoardOrderSoTabsOpenInTheOrderYouWorkThem()
    {
        var workstreams = new[]
        {
            Workstream("second", "Today", order: 2),
            Workstream("third", "In flight", order: 1),
            Workstream("first", "Today", order: 1),
        };

        Assert.That(
            StartSelection.ForBoard(workstreams, Columns, SessionRegistrySnapshot.Empty).Select(w => w.Key),
            Is.EqualTo(new[] { "first", "second", "third" }));
    }

    [Test]
    public void ForBoard_SkipsAWorkstreamWhoseSessionIsAlreadyRunning()
    {
        var workstreams = new[]
        {
            Workstream("contact-forms", "Today") with { LastSessionId = SessionId },
            Workstream("job-queue", "Today"),
        };

        var selected = StartSelection.ForBoard(workstreams, Columns, Live(SessionId));

        Assert.That(selected.Select(w => w.Key), Is.EqualTo(new[] { "job-queue" }));
    }

    [Test]
    public void ForBoard_StartsAWorkstreamWhoseLastSessionHasSinceEnded()
    {
        var workstreams = new[] { Workstream("contact-forms", "Today") with { LastSessionId = SessionId } };

        Assert.That(StartSelection.ForBoard(workstreams, Columns, SessionRegistrySnapshot.Empty), Has.Count.EqualTo(1));
    }

    [Test]
    public void ForBoard_SkipsArchivedWorkstreams()
    {
        var workstreams = new[] { Workstream("retired", "Today") with { IsArchived = true } };

        Assert.That(StartSelection.ForBoard(workstreams, Columns, SessionRegistrySnapshot.Empty), Is.Empty);
    }

    [Test]
    public void ForBoard_SkipsAWorkstreamWithNoPromptBecauseThereIsNothingToOpenWith()
    {
        var workstreams = new[] { Workstream("empty", "Today") with { Prompt = "  " } };

        Assert.That(StartSelection.ForBoard(workstreams, Columns, SessionRegistrySnapshot.Empty), Is.Empty);
    }

    [Test]
    public void ForBoard_StartsNothingWhenThereAreNoColumns()
    {
        Assert.That(
            StartSelection.ForBoard([Workstream("contact-forms", "Today")], [], SessionRegistrySnapshot.Empty),
            Is.Empty);
    }

    [Test]
    public void ForBoard_StartsNothingWhenTheOnlyLaneIsTheLastOne()
    {
        Assert.That(
            StartSelection.ForBoard([Workstream("only", "Today")], ["Today"],
                SessionRegistrySnapshot.Empty),
            Is.Empty);
    }

    [Test]
    public void ForColumn_StartsOnlyThatLane()
    {
        var workstreams = new[]
        {
            Workstream("contact-forms", "Today"),
            Workstream("job-queue", "In flight"),
        };

        Assert.That(
            StartSelection.ForColumn(workstreams, "In flight", SessionRegistrySnapshot.Empty)
                .Select(w => w.Key),
            Is.EqualTo(new[] { "job-queue" }));
    }

    [Test]
    public void ForColumn_WillStartTheLastLaneWhenThatLaneIsAskedForDirectly()
    {
        var workstreams = new[] { Workstream("finished", "Done") };

        Assert.That(
            StartSelection.ForColumn(workstreams, "Done", SessionRegistrySnapshot.Empty),
            Has.Count.EqualTo(1));
    }

    [Test]
    public void IsEligible_RefusesAWorkstreamThatIsAlreadyLive()
    {
        var workstream = Workstream("contact-forms", "Today") with { LastSessionId = SessionId };

        Assert.That(StartSelection.IsEligible(workstream, Live(SessionId)), Is.False);
    }

    private static SessionRegistrySnapshot Live(string sessionId) =>
        new([new LiveSession(4242, sessionId, @"C:\work\repo", "name", "busy", "cli", DateTimeOffset.UtcNow)], 0, 0);

    private static Workstream Workstream(string key, string column, int order = 1) =>
        new(key, key, "#208DD7", ColorIsManual: true, @"C:\work\repo", [], "Carry on.", order,
            column, IsBlocked: false, ReopenTomorrow: true, IsArchived: false, LastSessionId: null,
            [], null, DateTimeOffset.UtcNow);
}
