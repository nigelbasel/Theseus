using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>Which cards stop sharing a handover file, and how (handoff-names AC 1 to 4).</summary>
[TestFixture]
public class SharedHandoffsTests
{
    private const string Shared = @"C:\notes\HANDOFF-configure-the-widget-service.md";
    private static readonly DateOnly Today = new(2026, 11, 19);

    private static HandoffCard Owner(bool busy = false) =>
        new("configure-the-widget-service", "Configure the widget service", Shared, IsChosen: true, busy);

    private static HandoffCard Other(bool busy = false, bool chosen = true) =>
        new("widget-service", "Widget service", Shared, chosen, busy);

    private static IReadOnlyList<HandoffMove> Plan(IReadOnlyList<HandoffCard> cards, Func<string, bool>? exists = null,
        bool inRepository = false) =>
        SharedHandoffs.Plan(cards, exists ?? (_ => false), _ => inRepository, Today);

    [Test]
    public void TheOwnerKeepsTheFile_AndTheOtherCardGetsItsOwn_WithAPointer()
    {
        var move = Plan([Owner(), Other()]).Single();

        Assert.That(move.Key, Is.EqualTo("widget-service"));
        Assert.That(move.OwnFile, Is.EqualTo(@"C:\notes\HANDOFF-widget-service.md"));
        Assert.That(move.PointerText, Is.EqualTo(HandoffPointer.Text(Shared, Today)));
    }

    [Test]
    public void AnExistingOwnFile_IsNeverOverwritten()
    {
        var move = Plan([Owner(), Other()], exists: file => file.EndsWith("HANDOFF-widget-service.md")).Single();

        Assert.That(move.PointerText, Is.Null);
    }

    [Test]
    public void ABusyCard_IsLeftAlone()
    {
        Assert.That(Plan([Owner(), Other(busy: true)]), Is.Empty);
    }

    [Test]
    public void ABusyOwner_DoesNotStopTheOtherMoving()
    {
        Assert.That(Plan([Owner(busy: true), Other()]), Has.Count.EqualTo(1));
    }

    [Test]
    public void AFileNamedAfterNoSharer_MovesNobody()
    {
        Assert.That(Plan(
        [
            new HandoffCard("one", "One", @"C:\notes\HANDOFF.md", true, false),
            new HandoffCard("two", "Two", @"C:\notes\HANDOFF.md", true, false),
        ]), Is.Empty);
    }

    [Test]
    public void ACardThatFoundTheFileBySearch_IsNotMoved()
    {
        Assert.That(Plan([Owner(), Other(chosen: false)]), Is.Empty);
    }

    [Test]
    public void NothingIsMoved_InsideAGitWorkingTree()
    {
        Assert.That(Plan([Owner(), Other()], inRepository: true), Is.Empty);
    }

    [Test]
    public void AnOwnFileAnotherCardUses_IsNotTaken()
    {
        var third = new HandoffCard("third", "Third", @"C:\notes\HANDOFF-widget-service.md", true, false);

        Assert.That(Plan([Owner(), Other(), third]), Is.Empty);
    }

    [Test]
    public void AnOwnFileASearchingCardWouldFind_IsNotCreated()
    {
        var searching = new HandoffCard("widget", "Widget", @"C:\notes\HANDOFF-widget.md", false, false);

        Assert.That(Plan([Owner(), Other(), searching]), Is.Empty,
            "a card finding its notes by name would find the new file and start a second share");
    }

    [Test]
    public void TheNotice_NamesBothCardsAndBothFiles()
    {
        var notice = Plan([Owner(), Other()]).Single().Notice;

        Assert.That(notice, Does.Contain("Widget service").And.Contain("Configure the widget service")
            .And.Contain(Shared).And.Contain(@"C:\notes\HANDOFF-widget-service.md"));
    }
}
