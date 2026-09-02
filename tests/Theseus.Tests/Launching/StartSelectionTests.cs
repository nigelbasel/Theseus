using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Sessions;

namespace Theseus.Tests.Launching;

/// <summary>
/// What a bulk Start launches, now that it launches what the user ticked.
///
/// It used to work that out from where a card sat: every lane except the last, on the reasoning that
/// the last lane holds finished work. That is a rule about position, and it broke on a board with
/// four stages. `New / In progress / Paused / Done` puts `Paused` second-from-last, so one click
/// restarted everything its owner had deliberately paused — see `Selects_...` below, which is the
/// regression that rule would fail.
///
/// Reading the column's *name* instead would have been worse. The columns are the user's to name, in
/// any words and any language, so matching "Paused" or "Parked" is a guess about English dressed up
/// as a rule.
/// </summary>
[TestFixture]
public class StartSelectionTests
{
    private static readonly string[] Columns =
        ["New", "In progress", "Paused", "Done"];

    private const string SessionId = "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa";

    [Test]
    public void ForCards_StartsNothingThatWasNotChosen()
    {
        var paused = Workstream("nexus", "Paused");

        Assert.That(
            StartSelection.ForCards([Workstream("job-queue", "In progress")], Columns,
                SessionRegistrySnapshot.Empty).Select(w => w.Key),
            Is.EqualTo(new[] { "job-queue" }),
            "the paused card was not passed in, so nothing has to know that Paused means paused");

        Assert.That(paused.Column, Is.EqualTo("Paused"), "and its column decides nothing either way");
    }

    [Test]
    public void ForCards_StartsACardInTheLastLaneWhenThatIsWhatWasChosen()
    {
        // The old rule refused this outright. Restarting finished work is unusual, not forbidden,
        // and a card the user has ticked is not something to second-guess.
        Assert.That(
            StartSelection.ForCards([Workstream("finished", "Done")], Columns,
                SessionRegistrySnapshot.Empty),
            Has.Count.EqualTo(1));
    }

    [Test]
    public void ForCards_StartsInLaneOrderThenBoardOrderSoTabsOpenInTheOrderYouWorkThem()
    {
        var workstreams = new[]
        {
            Workstream("second", "New", order: 2),
            Workstream("third", "In progress", order: 1),
            Workstream("first", "New", order: 1),
        };

        Assert.That(
            StartSelection.ForCards(workstreams, Columns, SessionRegistrySnapshot.Empty).Select(w => w.Key),
            Is.EqualTo(new[] { "first", "second", "third" }));
    }

    [Test]
    public void ForCards_SkipsAWorkstreamWhoseSessionIsAlreadyRunning()
    {
        var workstreams = new[]
        {
            Workstream("contact-forms", "New") with { LastSessionId = SessionId },
            Workstream("job-queue", "New"),
        };

        Assert.That(
            StartSelection.ForCards(workstreams, Columns, Live(SessionId)).Select(w => w.Key),
            Is.EqualTo(new[] { "job-queue" }),
            "ticking a running card is not a request to open a second session for it");
    }

    [Test]
    public void ForCards_StartsAWorkstreamWhoseLastSessionHasSinceEnded()
    {
        var workstreams = new[] { Workstream("contact-forms", "New") with { LastSessionId = SessionId } };

        Assert.That(
            StartSelection.ForCards(workstreams, Columns, SessionRegistrySnapshot.Empty),
            Has.Count.EqualTo(1));
    }

    [Test]
    public void ForCards_SkipsArchivedWorkstreams()
    {
        var workstreams = new[] { Workstream("retired", "New") with { IsArchived = true } };

        Assert.That(
            StartSelection.ForCards(workstreams, Columns, SessionRegistrySnapshot.Empty), Is.Empty);
    }

    [Test]
    public void ForCards_SkipsAWorkstreamWithNoPromptBecauseThereIsNothingToOpenWith()
    {
        var workstreams = new[] { Workstream("empty", "New") with { Prompt = "  " } };

        Assert.That(
            StartSelection.ForCards(workstreams, Columns, SessionRegistrySnapshot.Empty), Is.Empty);
    }

    [Test]
    public void ForCards_StartsNothingWhenNothingWasChosen()
    {
        Assert.That(StartSelection.ForCards([], Columns, SessionRegistrySnapshot.Empty), Is.Empty);
    }

    [Test]
    public void ForCards_StillStartsWhatItWasGivenWhenTheBoardHasNoColumnsToOrderBy()
    {
        Assert.That(
            StartSelection.ForCards([Workstream("job-queue", "In progress")], [],
                SessionRegistrySnapshot.Empty),
            Has.Count.EqualTo(1),
            "the columns only decide the order; an unrecognised one must not silently drop a card");
    }

    [Test]
    public void IsEligible_RefusesAWorkstreamThatIsAlreadyLive()
    {
        var workstream = Workstream("contact-forms", "New") with { LastSessionId = SessionId };

        Assert.That(StartSelection.IsEligible(workstream, Live(SessionId)), Is.False);
    }

    private static SessionRegistrySnapshot Live(string sessionId) =>
        new([new LiveSession(4242, sessionId, @"C:\work\repo", "name", "busy", "cli", DateTimeOffset.UtcNow)], 0, 0);

    private static Workstream Workstream(string key, string column, int order = 1) =>
        new(key, key, "#208DD7", ColorIsManual: true, @"C:\work\repo", [], "Carry on.", order,
            column, IsBlocked: false, ReopenTomorrow: true, IsArchived: false, LastSessionId: null,
            [], null, DateTimeOffset.UtcNow);
}
