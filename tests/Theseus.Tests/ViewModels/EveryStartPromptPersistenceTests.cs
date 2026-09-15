namespace Theseus.Tests.ViewModels;

/// <summary>
/// A workstream's own every-start instructions live in the board file, not the plan file, because
/// sessions rewrite their plan files. So they have to survive the board writing a card back and
/// every refresh after it, which rebuilds each card from the files.
/// </summary>
[TestFixture]
public class EveryStartPromptPersistenceTests
{
    [Test]
    public void Persist_KeepsAWorkstreamsOwnInstructionsAcrossARefresh()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();

        var card = fixture.Card("one")!;
        card.Workstream = card.Workstream with { EveryStartPrompt = "Run the tests before committing." };
        fixture.Board.Persist(card);

        fixture.Board.Refresh();

        Assert.That(fixture.Card("one")!.Workstream.EveryStartPrompt, Is.EqualTo("Run the tests before committing."));
    }

    [Test]
    public void Persist_ClearsThemWhenTheyAreRemoved()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();

        var card = fixture.Card("one")!;
        card.Workstream = card.Workstream with { EveryStartPrompt = "Something." };
        fixture.Board.Persist(card);
        fixture.Board.Refresh();

        card = fixture.Card("one")!;
        card.Workstream = card.Workstream with { EveryStartPrompt = null };
        fixture.Board.Persist(card);
        fixture.Board.Refresh();

        Assert.That(fixture.Card("one")!.Workstream.EveryStartPrompt, Is.Null);
    }
}
