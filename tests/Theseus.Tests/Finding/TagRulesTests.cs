using Theseus.Core.Finding;

namespace Theseus.Tests.Finding;

/// <summary>The tags' own rules (card-search-and-tags items 10, 11, 13): palette, what a card shows, and list changes.</summary>
[TestFixture]
public class TagRulesTests
{
    [Test]
    public void ANewTag_TakesTheLeastUsedColour()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TagPalette.Next([]), Is.EqualTo(TagPalette.Colours[0]));
            Assert.That(TagPalette.Next([TagPalette.Colours[0]]), Is.EqualTo(TagPalette.Colours[1]));
        });
    }

    [Test]
    public void Assign_GivesOnlyTheMissingTagsAColour_AndKeepsTheRest()
    {
        var colours = new Dictionary<string, string> { ["bym"] = "#123456" };

        var assigned = TagPalette.Assign(colours, ["bym", "security"]);

        Assert.Multiple(() =>
        {
            Assert.That(assigned["bym"], Is.EqualTo("#123456"));
            Assert.That(TagPalette.Colours, Does.Contain(assigned["security"]));
            Assert.That(TagPalette.Assign(assigned, ["bym"]), Is.SameAs(assigned), "nothing missing, nothing to write");
        });
    }

    [Test]
    public void ACard_ShowsThreeTags_ThenPlusTheRest_WithTheRestInItsTooltip()
    {
        IReadOnlyList<string> tags = ["a", "b", "c", "d", "e"];

        Assert.Multiple(() =>
        {
            Assert.That(TagDisplay.Visible(tags), Is.EqualTo(new[] { "a", "b", "c" }));
            Assert.That(TagDisplay.More(tags), Is.EqualTo("+2"));
            Assert.That(TagDisplay.Rest(tags), Is.EqualTo($"#d{Environment.NewLine}#e"));
            Assert.That(TagDisplay.More(["a"]), Is.Empty);
        });
    }

    [Test]
    public void With_AndWithout_KeepTheOrder_AndAddOnce()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TagLists.With(["a"], "b"), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(TagLists.With(["a"], "a"), Is.EqualTo(new[] { "a" }));
            Assert.That(TagLists.Without(["a", "b"], "a"), Is.EqualTo(new[] { "b" }));
        });
    }

    [Test]
    public void Renamed_OntoATagTheCardHas_MergesThem()
    {
        Assert.That(TagLists.Renamed(["old", "new", "x"], "old", "new"), Is.EqualTo(new[] { "new", "x" }));
    }

    [Test]
    public void Counts_SayHowManyCardsCarryEachTag_Alphabetically()
    {
        var counts = TagLists.Counts([["bym", "security"], ["bym"], []]);

        Assert.That(counts, Is.EqualTo(new[] { new TagCount("bym", 2), new TagCount("security", 1) }));
    }
}
