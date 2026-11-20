using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The re-review of PR 2's fixes: a ticked card that leaves the grid is unticked, and the selected-card commands take only
/// shown cards (item 1).
/// </summary>
[TestFixture]
public class ReReviewFixTests
{
    private static BoardFixture Board()
    {
        var fixture = new BoardFixture().With("ad-hoc").With("member");
        fixture.Board.Refresh();

        return fixture;
    }

    [Test]
    public void ATickedCard_WhoseProjectBeginsToRead_IsUnticked()
    {
        var fixture = Board();
        var member = fixture.Card("member")!;
        member.Workstream = member.Workstream with { Project = "p", Slug = "a-slug" };
        fixture.Board.Persist(member);
        member.IsSelected = true;

        fixture.Board.KeepOutOfGrid = card => card.Workstream.Project == "p";
        fixture.Board.Refresh();

        Assert.That(fixture.Card("member")!.IsSelected, Is.False, "the first catalogue landing took it out of sight");
        Assert.That(fixture.Board.ChosenCards, Is.Empty);
    }

    [Test]
    public void ATickedCard_ThatJoinsAReadableProject_IsUnticked()
    {
        var fixture = Board();
        fixture.Board.KeepOutOfGrid = card => card.Workstream.Project == "p";
        fixture.Board.Refresh();
        var member = fixture.Card("member")!;
        member.IsSelected = true;

        member.Workstream = member.Workstream with { Project = "p", Slug = "a-slug" };
        fixture.Board.Persist(member);
        fixture.Board.Refresh();

        Assert.That(fixture.Card("member")!.IsSelected, Is.False, "SetProject's refresh took it out of sight");
    }

    [Test]
    public void TheSelectedCardCommands_TakeOnlyShownCards_EvenIfAHiddenOneIsTicked()
    {
        var fixture = Board();
        var member = fixture.Card("member")!;
        member.Workstream = member.Workstream with { Project = "p", Slug = "a-slug" };
        fixture.Board.Persist(member);
        fixture.Board.KeepOutOfGrid = card => card.Workstream.Project == "p";
        fixture.Board.Refresh();
        fixture.Card("ad-hoc")!.IsSelected = true;

        fixture.Card("member")!.IsSelected = true;

        Assert.That(fixture.Board.ChosenCards.Select(card => card.Key), Is.EqualTo(new[] { "ad-hoc" }));
    }
}
