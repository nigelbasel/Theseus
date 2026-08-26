using Theseus.Core.Domain;
using Theseus.Core.Launching;

namespace Theseus.Tests.Launching;

[TestFixture]
public class WorkstreamIdentityTests
{
    private const string Handoff = @"C:\dev\tfsgit\SESSION-STATUS-2026-08-26-contact-forms.md";

    [Test]
    public void For_TellsTheSessionWhichWorkstreamItIs()
    {
        var identity = WorkstreamIdentity.For(Workstream(), Handoff);

        Assert.That(identity, Does.Contain("Contact form validation"));
        Assert.That(identity, Does.Contain("contact-forms"));
    }

    [Test]
    public void For_TellsItWhichNotesAreItsOwn()
    {
        Assert.That(WorkstreamIdentity.For(Workstream(), Handoff), Does.Contain(Handoff));
    }

    [Test]
    public void For_WarnsThatTheNotesNearbyBelongToOtherWorkstreams()
    {
        var identity = WorkstreamIdentity.For(Workstream(), Handoff);

        Assert.That(identity, Does.Contain("Other workstreams"));
        Assert.That(identity, Does.Contain("say so rather than doing it"),
            "the failure was a session doing work it found rather than reporting it");
    }

    [Test]
    public void For_FallsBackToTheKeyWhenAWorkstreamHasNoTitle()
    {
        var identity = WorkstreamIdentity.For(Workstream() with { Title = "  " }, Handoff);

        Assert.That(identity, Does.Contain("\"contact-forms\""));
    }

    private static Workstream Workstream() =>
        new("contact-forms", "Contact form validation", "#208DD7", ColorIsManual: true,
            @"C:\dev\tfsgit", [], "Add validation.", 1, "Today", IsBlocked: false,
            ReopenTomorrow: true, IsArchived: false, "session-1", [], null, DateTimeOffset.UtcNow);
}
