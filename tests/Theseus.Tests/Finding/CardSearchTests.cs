using Theseus.Core.Finding;

namespace Theseus.Tests.Finding;

[TestFixture]
public class CardSearchTests
{
    private static SearchableCard Card(
        string title = "A card",
        string key = "a-card",
        string project = "",
        string area = "",
        string[]? jira = null,
        string[]? tags = null,
        string[]? branches = null,
        string[]? repos = null) =>
        new(key, title, project, area, jira ?? [], tags ?? [], branches ?? [], repos ?? []);

    private static bool Finds(string typed, SearchableCard card) => CardSearch.Matches(SearchQuery.Parse(typed).Words, card);

    [Test]
    public void ATitle_MatchesAnyPartOfAnyWord_IgnoringCase()
    {
        Assert.That(Finds("ARCH", Card(title: "Card search and tags")), Is.True);
    }

    [Test]
    public void TheKey_IsMatched()
    {
        Assert.That(Finds("1-3-find", Card(title: "Something else", key: "theseus-1-3-finding-cards")), Is.True);
    }

    [Test]
    public void TheProject_IsMatched()
    {
        Assert.That(Finds("buildyour", Card(project: "BuildYourMarket MCP")), Is.True);
    }

    [Test]
    public void TheArea_IsMatched()
    {
        Assert.That(Finds("infra", Card(area: "Infrastructure")), Is.True);
    }

    [Test]
    public void AJiraKey_IsMatched()
    {
        Assert.That(Finds("abc-12", Card(jira: ["ABC-123"])), Is.True);
    }

    [Test]
    public void ATag_IsMatchedAsAWord()
    {
        Assert.That(Finds("waiting", Card(tags: ["waiting-on-dpo"])), Is.True);
    }

    [Test]
    public void ABranch_IsMatched()
    {
        Assert.That(Finds("search-and", Card(branches: ["user/card-search-and-tags"])), Is.True);
    }

    [Test]
    public void ARepo_IsMatched()
    {
        Assert.That(Finds("theseus", Card(repos: ["Theseus"])), Is.True);
    }

    [Test]
    public void SeveralWords_MustAllMatch_EachInAnyField()
    {
        var card = Card(title: "BYM MCP W7 - GUI parity, search-backed reads");

        Assert.Multiple(() =>
        {
            Assert.That(Finds("bym search", card), Is.True, "the spec's own example");
            Assert.That(Finds("bym missing", card), Is.False, "one word not found fails the card");
            Assert.That(Finds("w7 tag", Card(title: "W7", tags: ["tagged"])), Is.True, "words may match different fields");
        });
    }

    [Test]
    public void NoWords_MatchesEveryCard()
    {
        Assert.That(CardSearch.Matches([], Card()), Is.True);
    }

    [Test]
    public void Highlights_CoverEachOccurrence_AndJoinOverlaps()
    {
        var spans = CardSearch.Highlights("search research", ["sea", "earch"]);

        Assert.That(spans, Is.EqualTo(new[] { new TextSpan(0, 6), new TextSpan(9, 6) }));
    }

    [Test]
    public void Highlights_AreEmptyWithNoWordsOrNoText()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CardSearch.Highlights("title", []), Is.Empty);
            Assert.That(CardSearch.Highlights(string.Empty, ["a"]), Is.Empty);
        });
    }

    [Test]
    public void Highlights_FindAWordAtTheVeryEnd()
    {
        Assert.That(CardSearch.Highlights("ab", ["b"]), Is.EqualTo(new[] { new TextSpan(1, 1) }));
    }
}
