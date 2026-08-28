using Theseus.Core.Domain;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

[TestFixture]
public class HandoffPinTests
{
    [Test]
    public void Pin_SettlesAPathForAWorkstreamThatHasNone()
    {
        var pinned = HandoffPin.Pin(Workstream());

        Assert.That(pinned.StatusDocumentPath, Is.EqualTo(@"C:\work\repo\HANDOFF-contact-forms.md"));
    }

    [Test]
    public void Pin_LeavesAWorkstreamThatAlreadyHasOneExactlyAsItWas()
    {
        var settled = Workstream() with { StatusDocumentPath = @"C:\notes\HANDOFF-contact-forms.md" };

        Assert.That(HandoffPin.Pin(settled), Is.SameAs(settled),
            "a path is chosen once; revising it underneath a running session strands its notes");
    }

    [Test]
    public void Pin_IsTheSameAnswerTheSecondTimeAsTheFirst()
    {
        var once = HandoffPin.Pin(Workstream());
        var twice = HandoffPin.Pin(once);

        Assert.That(twice.StatusDocumentPath, Is.EqualTo(once.StatusDocumentPath),
            "launch and stop both pin, and they have to agree or the notes go astray");
    }

    private static Workstream Workstream() => new(
        "contact-forms", "Contact forms", "#336699", ColorIsManual: false, @"C:\work\repo", [],
        "prompt", 1, "Doing", IsBlocked: false, ReopenTomorrow: false, IsArchived: false,
        LastSessionId: null, [], StatusDocumentPath: null, DateTimeOffset.UtcNow);
}
