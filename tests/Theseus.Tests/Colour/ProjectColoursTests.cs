using Theseus.Core.Colour;

namespace Theseus.Tests.Colour;

/// <summary>
/// Each project's own colour (1.1d AC 40; decision 7, 2026-11-21): from a project palette none of whose colours is a
/// workstream colour, so however many workstreams there are a project never shares a card's colour; two projects avoid
/// each other's while the palette has room; a saved project colour is kept, and one from outside the palette given again.
/// </summary>
[TestFixture]
public class ProjectColoursTests
{
    [Test]
    public void TheProjectPalette_SharesNoColourWithTheWorkstreamPalette()
    {
        Assert.That(ProjectColourPalette.All, Has.Count.EqualTo(8));
        Assert.That(ProjectColourPalette.All.Where(TabColourPalette.Contains), Is.Empty);
    }

    [Test]
    public void ABoardWhoseWorkstreamsHoldEveryWorkstreamColour_StillGivesAProjectAColourNoWorkstreamHas()
    {
        // About a hundred workstreams: every one of the sixteen workstream colours held six times over.
        var workstreams = Enumerable.Range(0, 100).Select(index => TabColourPalette.All[index % TabColourPalette.All.Count]).ToList();

        var colours = ProjectColours.Assign(new Dictionary<string, string>(), ["theseus", "bym"]);

        Assert.That(colours.Values, Has.None.AnyOf(workstreams.Distinct().ToArray()));
        Assert.That(colours.Values, Is.All.Matches<string>(ProjectColourPalette.Contains));
    }

    [Test]
    public void TwoProjects_AvoidEachOthersColour_WhileThePaletteHasRoom()
    {
        var names = Enumerable.Range(1, 8).Select(index => $"p{index}").ToList();

        var colours = ProjectColours.Assign(new Dictionary<string, string> { ["p3"] = ProjectColourPalette.All[0] }, names);

        Assert.That(colours.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(8));
        Assert.That(colours["p3"], Is.EqualTo(ProjectColourPalette.All[0]), "a colour once given is kept");
    }

    [Test]
    public void ASavedColourFromOutsideTheProjectPalette_IsGivenAgain_AndOneInsideItIsKept()
    {
        IReadOnlyDictionary<string, string> saved = new Dictionary<string, string> { ["old"] = "#208DD7", ["Theseus"] = "#5E4BB0" };

        var colours = ProjectColours.Assign(saved, ["old", "theseus"]);

        Assert.That(ProjectColours.Of(saved, "old"), Is.Null, "a workstream colour is not a project's");
        Assert.That(colours["old"], Is.Not.EqualTo("#208DD7").And.Matches<string>(ProjectColourPalette.Contains));
        Assert.That(colours["old"], Is.Not.EqualTo("#5E4BB0"));
        Assert.That(ProjectColours.Of(colours, "THESEUS"), Is.EqualTo("#5E4BB0"));
    }

    [Test]
    public void WithEveryProjectColoured_NothingChanges_AndTheSameColoursComeBack()
    {
        IReadOnlyDictionary<string, string> saved = new Dictionary<string, string> { ["Theseus"] = "#5E4BB0" };

        Assert.That(ProjectColours.Assign(saved, ["theseus"]), Is.SameAs(saved));
    }

    [Test]
    public void ColoursAreGivenInNameOrder_WhateverOrderTheProjectsAreRead()
    {
        var one = ProjectColours.Assign(new Dictionary<string, string>(), ["b", "a", "c"]);
        var other = ProjectColours.Assign(new Dictionary<string, string>(), ["c", "a", "b"]);

        Assert.That(one.OrderBy(entry => entry.Key), Is.EqualTo(other.OrderBy(entry => entry.Key)));
    }
}
