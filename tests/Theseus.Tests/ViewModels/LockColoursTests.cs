using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Locks;

namespace Theseus.Tests.ViewModels;

[TestFixture]
public class LockColoursTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    private static readonly WorkstreamCardViewModel[] Cards =
    [
        Card("nexus-admin", "Nexus Admin", "#208DD7", "session-nexus"),
        Card("legacy-jobs", "Legacy job migration", "#C0392B", "session-legacy"),
    ];

    [Test]
    public void ASessionHold_IsMatchedBySessionEvenWhenTheNameDiffers()
    {
        var ticket = Ticket("someone else", sessionId: "session-legacy");

        Assert.That(LockColours.For(ticket, Cards), Is.EqualTo("#C0392B"));
    }

    [Test]
    public void ATestRun_IsMatchedByTheCardTitleItGaveAsItsName()
    {
        Assert.That(LockColours.For(Ticket("Nexus Admin"), Cards), Is.EqualTo("#208DD7"));
        Assert.That(LockColours.For(Ticket("nexus admin"), Cards), Is.EqualTo("#208DD7"));
    }

    [Test]
    public void ATestRun_IsMatchedByWorkstreamKeyToo()
    {
        Assert.That(LockColours.For(Ticket("legacy-jobs"), Cards), Is.EqualTo("#C0392B"));
    }

    [Test]
    public void NoMatch_GivesNoColourRatherThanAGuess()
    {
        Assert.That(LockColours.For(Ticket("Theseus check"), Cards), Is.Null);
    }

    private static LockTicket Ticket(string who, string? sessionId = null) =>
        new("t", LockNames.Tests, who, "suite", sessionId is null ? HoldMode.Process : HoldMode.Session, "k", Nine, Nine, SessionId: sessionId);

    private static WorkstreamCardViewModel Card(string key, string title, string colour, string sessionId) =>
        new(new Workstream(key, title, colour, ColorIsManual: true, @"C:\work", [], "Prompt.",
            1, "New", IsBlocked: false, ReopenTomorrow: true, IsArchived: false, sessionId, [], null, Nine));
}
