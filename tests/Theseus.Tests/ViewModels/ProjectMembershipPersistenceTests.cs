namespace Theseus.Tests.ViewModels;

/// <summary>
/// A card's project and slug live in the board file (project-view AC 4), so they have to survive the board writing the
/// card back for any other reason, and the refresh after it (P21).
/// </summary>
[TestFixture]
public class ProjectMembershipPersistenceTests
{
    [Test]
    public void ACardWithNoMembershipSet_IsAdHoc()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();

        Assert.That((fixture.Card("one")!.Workstream.Project, fixture.Card("one")!.Workstream.Slug),
            Is.EqualTo(((string?)null, (string?)null)));
    }

    [Test]
    public void Persist_KeepsTheProjectAndSlugAcrossARefresh()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();

        var card = fixture.Card("one")!;
        card.Workstream = card.Workstream with { Project = "a-project", Slug = "a-slug" };
        fixture.Board.Persist(card);
        fixture.Board.Refresh();

        Assert.That((fixture.Card("one")!.Workstream.Project, fixture.Card("one")!.Workstream.Slug),
            Is.EqualTo(("a-project", "a-slug")));
    }

    [Test]
    public void Persist_OfAnotherChange_DoesNotDropTheMembership()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();

        var card = fixture.Card("one")!;
        card.Workstream = card.Workstream with { Project = "a-project", Slug = "a-slug" };
        fixture.Board.Persist(card);
        fixture.Board.Refresh();

        card = fixture.Card("one")!;
        card.Workstream = card.Workstream with { IsBlocked = true };
        fixture.Board.Persist(card);
        fixture.Board.Refresh();

        Assert.That(fixture.Card("one")!.Workstream.Slug, Is.EqualTo("a-slug"));
    }
}
