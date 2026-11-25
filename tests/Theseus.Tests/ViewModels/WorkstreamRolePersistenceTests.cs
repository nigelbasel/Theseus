using Theseus.Core.Domain;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A card's role lives in the board file, not the plan file, because sessions rewrite their plan files and would drop
/// a field they do not know about. So it has to survive the board writing the card back, and the refresh after it
/// (verifier-card AC 1).
/// </summary>
[TestFixture]
public class WorkstreamRolePersistenceTests
{
    [Test]
    public void ACardWithNoRoleSet_HasNone()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();

        Assert.That(fixture.Card("one")!.Workstream.Role, Is.EqualTo(WorkstreamRole.None));
    }

    [Test]
    public void Persist_KeepsAVerifiersRoleAcrossARefresh()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();

        var card = fixture.Card("one")!;
        card.Workstream = card.Workstream with { Role = WorkstreamRole.Verifier };
        fixture.Board.Persist(card);
        fixture.Board.Refresh();

        Assert.That(fixture.Card("one")!.Workstream.Role, Is.EqualTo(WorkstreamRole.Verifier));
    }

    [Test]
    public void Persist_OfAnotherChange_DoesNotDropTheRole()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();

        var card = fixture.Card("one")!;
        card.Workstream = card.Workstream with { Role = WorkstreamRole.Verifier };
        fixture.Board.Persist(card);
        fixture.Board.Refresh();

        card = fixture.Card("one")!;
        card.Workstream = card.Workstream with { IsBlocked = true };
        fixture.Board.Persist(card);
        fixture.Board.Refresh();

        Assert.That(fixture.Card("one")!.Workstream.Role, Is.EqualTo(WorkstreamRole.Verifier),
            "every write of the card rebuilds its board entry; one that forgot the role would quietly demote it (P21)");
    }
}
