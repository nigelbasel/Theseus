using Theseus.Core.Finding;

namespace Theseus.Tests.Finding;

[TestFixture]
public class CardFilterTests
{
    private static SearchableCard Card(string title, params string[] tags) => new(title, title, "", "", [], tags, [], []);

    [Test]
    public void TickedTags_InAny_AdmitACardWithAnyOfThem()
    {
        var filter = new CardFilter(SearchQuery.Empty, ["bym", "security"], TagMatch.Any);

        Assert.Multiple(() =>
        {
            Assert.That(filter.Admits(Card("one", "bym")), Is.True);
            Assert.That(filter.Admits(Card("two", "other")), Is.False);
        });
    }

    [Test]
    public void TickedTags_InAll_AdmitOnlyACardWithEveryOne()
    {
        var filter = new CardFilter(SearchQuery.Empty, ["bym", "security"], TagMatch.All);

        Assert.Multiple(() =>
        {
            Assert.That(filter.Admits(Card("one", "bym")), Is.False);
            Assert.That(filter.Admits(Card("two", "security", "bym")), Is.True);
        });
    }

    [Test]
    public void AHashTagInTheBox_IsTheSameAsTickingIt()
    {
        var typed = new CardFilter(SearchQuery.Parse("#bym"), [], TagMatch.Any);
        var ticked = new CardFilter(SearchQuery.Empty, ["bym"], TagMatch.Any);

        Assert.Multiple(() =>
        {
            Assert.That(typed.Admits(Card("x", "bym")), Is.EqualTo(ticked.Admits(Card("x", "bym"))));
            Assert.That(typed.Admits(Card("y")), Is.EqualTo(ticked.Admits(Card("y"))));
            Assert.That(typed.Tags, Is.EqualTo(ticked.Tags));
        });
    }

    [Test]
    public void WordsAndTags_Combine_BothMustHold()
    {
        var filter = new CardFilter(SearchQuery.Parse("#bym search"), [], TagMatch.Any);

        Assert.Multiple(() =>
        {
            Assert.That(filter.Admits(Card("search reads", "bym")), Is.True);
            Assert.That(filter.Admits(Card("search reads")), Is.False, "the words match but the tag does not");
            Assert.That(filter.Admits(Card("other", "bym")), Is.False, "the tag matches but the words do not");
        });
    }

    [Test]
    public void None_IsOff_AndAdmitsEverything()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CardFilter.None.IsOn, Is.False);
            Assert.That(CardFilter.None.Admits(Card("anything")), Is.True);
            Assert.That(CardFilter.None.Describe(3, 10), Is.Empty);
        });
    }

    [Test]
    public void Describe_SaysTheTagsTheWordsAndTheCount()
    {
        var filter = new CardFilter(SearchQuery.Parse("search"), ["bym"], TagMatch.Any);

        Assert.That(filter.Describe(3, 112), Is.EqualTo("Filtered: #bym · 'search' · 3 of 112"));
    }

    [Test]
    public void Describe_JoinsTagsByOrInAny_AndByPlusInAll()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new CardFilter(SearchQuery.Empty, ["a", "b"], TagMatch.Any).Describe(1, 2), Does.StartWith("Filtered: #a or #b"));
            Assert.That(new CardFilter(SearchQuery.Empty, ["a", "b"], TagMatch.All).Describe(1, 2), Does.StartWith("Filtered: #a + #b"));
        });
    }
}
