using Theseus.Core.Domain;
using Theseus.Core.Sessions;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

[TestFixture]
public class StopSelectionTests
{
    private const string SessionA = "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa";
    private const string SessionB = "8c1d0e91-4b6a-4f22-9e03-71ab2c4d66bb";

    private static readonly Dictionary<string, string> Names = new()
    {
        [SessionA] = "Contact form validation",
        [SessionB] = "Job queue migration",
    };

    [Test]
    public void ForWorkstream_StopsTheSessionTheWorkstreamFollows()
    {
        var targets = StopSelection.ForWorkstream(
            Workstream("contact-forms", SessionA), Live(SessionA), Names);

        Assert.That(targets, Has.Count.EqualTo(1));
        Assert.That(targets[0].SessionName, Is.EqualTo("Contact form validation"));
    }

    [Test]
    public void ForWorkstream_PrefersTheWorkstreamOwnHandoverDocument()
    {
        var workstream = Workstream("contact-forms", SessionA)
            with { StatusDocumentPath = @"C:\work\repo\SESSION-STATUS-contact-forms.md" };

        var targets = StopSelection.ForWorkstream(workstream, Live(SessionA), Names);

        Assert.That(targets[0].HandoffPath, Is.EqualTo(@"C:\work\repo\SESSION-STATUS-contact-forms.md"));
    }

    [Test]
    public void ForWorkstream_NamesAHandoffBesideTheWorkingDirectoryWhenThereIsNoStatusDocument()
    {
        var targets = StopSelection.ForWorkstream(
            Workstream("contact-forms", SessionA), Live(SessionA), Names);

        Assert.That(targets[0].HandoffPath, Is.EqualTo(@"C:\work\repo\HANDOFF-contact-forms.md"),
            "undated, so tomorrow's session is sent to the file today's session was told to write");
    }

    [Test]
    public void ForWorkstream_StopsNothingWhenTheSessionHasAlreadyEnded()
    {
        var targets = StopSelection.ForWorkstream(
            Workstream("contact-forms", SessionA), SessionRegistrySnapshot.Empty, Names);

        Assert.That(targets, Is.Empty);
    }

    [Test]
    public void ForWorkstream_StopsNothingForAWorkstreamThatWasNeverStarted()
    {
        var targets = StopSelection.ForWorkstream(
            Workstream("contact-forms", sessionId: null), Live(SessionA), Names);

        Assert.That(targets, Is.Empty);
    }

    [Test]
    public void ForWorkstream_StopsNothingWhenTheSessionCannotBeAddressedByName()
    {
        var targets = StopSelection.ForWorkstream(
            Workstream("contact-forms", SessionA), Live(SessionA), new Dictionary<string, string>());

        Assert.That(targets, Is.Empty);
    }

    [Test]
    public void ForBoard_StopsEveryWorkstreamThatIsRunning()
    {
        var workstreams = new[]
        {
            Workstream("contact-forms", SessionA),
            Workstream("job-queue", SessionB),
            Workstream("never-started", sessionId: null),
        };

        var targets = StopSelection.ForBoard(workstreams, Live(SessionA, SessionB), Names);

        Assert.That(targets.Select(t => t.SessionName),
            Is.EqualTo(new[] { "Contact form validation", "Job queue migration" }));
    }

    [Test]
    public void ForBoard_AddressesASharedSessionOnceRatherThanTwice()
    {
        var workstreams = new[]
        {
            Workstream("contact-forms", SessionA),
            Workstream("also-contact-forms", SessionA),
        };

        Assert.That(StopSelection.ForBoard(workstreams, Live(SessionA), Names), Has.Count.EqualTo(1));
    }

    [Test]
    public void ForBoard_StopsNothingWhenTheBoardIsIdle()
    {
        Assert.That(
            StopSelection.ForBoard([Workstream("contact-forms", SessionA)],
                SessionRegistrySnapshot.Empty, Names),
            Is.Empty);
    }

    private static SessionRegistrySnapshot Live(params string[] sessionIds) =>
        new([.. sessionIds.Select((id, index) =>
            new LiveSession(4242 + index, id, @"C:\work\repo", "name", "idle", "cli", DateTimeOffset.UtcNow))],
            0, 0);

    private static Workstream Workstream(string key, string? sessionId) =>
        new(key, key, "#208DD7", ColorIsManual: true, @"C:\work\repo", [], "Carry on.", 1,
            "Today", IsBlocked: false, ReopenTomorrow: true, IsArchived: false,
            sessionId, [], null, DateTimeOffset.UtcNow);
}
