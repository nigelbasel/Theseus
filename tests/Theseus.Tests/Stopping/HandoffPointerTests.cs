using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>The one line a card's new handover file starts with, after a shared file is resolved (handoff-names AC 2).</summary>
[TestFixture]
public class HandoffPointerTests
{
    [Test]
    public void ItNamesTheSharedFileAndTheDate()
    {
        Assert.That(HandoffPointer.Text(@"C:\notes\HANDOFF-shared.md", new DateOnly(2026, 11, 19)), Is.EqualTo(
            @"Until 2026-11-19 this card shared its handover notes with another card, in C:\notes\HANDOFF-shared.md. Its notes are kept here from now on."));
    }

    [Test]
    public void ACardsOwnFile_IsNamedForItsKey_BesideTheSharedOne()
    {
        Assert.That(HandoffPointer.OwnFile(@"C:\notes\HANDOFF-shared.md", "widget-service"),
            Is.EqualTo(@"C:\notes\HANDOFF-widget-service.md"));
    }
}
