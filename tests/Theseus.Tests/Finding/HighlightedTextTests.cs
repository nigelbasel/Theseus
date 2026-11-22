using System.Windows.Controls;
using System.Windows.Documents;
using Theseus.App.Controls;
using Theseus.Core.Finding;

namespace Theseus.Tests.Finding;

/// <summary>A title with the search's matches highlighted in it (card-search-and-tags item 3).</summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class HighlightedTextTests
{
    [Test]
    public void TheMatchedRuns_AreHighlighted_AndTheRestIsPlain()
    {
        var block = new TextBlock();

        HighlightedText.SetText(block, "BYM MCP W7");
        HighlightedText.SetSpans(block, [new TextSpan(4, 3)]);
        var runs = block.Inlines.OfType<Run>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(runs.Select(run => run.Text), Is.EqualTo(new[] { "BYM ", "MCP", " W7" }));
            Assert.That(runs[1].ReadLocalValue(TextElement.BackgroundProperty), Is.Not.EqualTo(System.Windows.DependencyProperty.UnsetValue),
                "the match takes the theme's highlight");
            Assert.That(runs[0].ReadLocalValue(TextElement.BackgroundProperty), Is.EqualTo(System.Windows.DependencyProperty.UnsetValue));
            Assert.That(System.Windows.Automation.AutomationProperties.GetName(block), Is.EqualTo("BYM MCP W7"));
        });
    }

    [Test]
    public void NoMatches_IsThePlainText()
    {
        var block = new TextBlock();

        HighlightedText.SetText(block, "A title");
        HighlightedText.SetSpans(block, []);

        Assert.That(block.Text, Is.EqualTo("A title"));
    }
}
