using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Theseus.App.Controls;
using Theseus.App.Markdown;
using Theseus.App.Text;

namespace Theseus.Tests.Conventions;

/// <summary>
/// The text built in code follows the same ramp, font and grid as the markup (baseline W18, W21, W23, W36, W50; findings
/// A25, A26, A28, A29, D9, D17, E1, E10).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class SharedTextTests
{
    [Test]
    public void ASettingRow_IsBodyStrongOverCaption()
    {
        SettingRow row = new() { Label = "Theme", Value = "Dark", Explanation = "How Theseus looks." };

        Assert.That(row.Lines.Select(line => (line.FontSize, line.FontWeight)),
            Is.EqualTo(new[] { (14.0, FontWeights.SemiBold), (12.0, FontWeights.Normal), (12.0, FontWeights.Normal) }), "D17, A25");
        Assert.That(row.Lines.Select(line => line.Margin.Top).Concat([row.Margin.Bottom]), Has.All.Matches<double>(value => value % 4 == 0));
    }

    [Test]
    public void MarkdownIsDrawnInTheUiFontAtBody_NoWiderThanAReadingWidth_WithHeadingsOnTheRamp()
    {
        FlowDocument document = MarkdownRenderer.Render("# One\n\n## Two\n\n### Three\n\nText.");
        double[] headings = [.. document.Blocks.OfType<Paragraph>().Take(3).Select(paragraph => paragraph.FontSize)];

        Assert.That((document.FontFamily.Source, document.FontSize, document.MaxPageWidth),
            Is.EqualTo((TypeRamp.UiFont.Source, 14.0, 640.0)), "A26, A28, A29");
        Assert.That(headings, Is.EqualTo(new[] { 20.0, 18.0, 14.0 }));
    }

    [Test]
    public void MarkdownSpacing_IsOnTheFourGrid()
    {
        FlowDocument document = MarkdownRenderer.Render("Text.\n\n- one\n- two\n\n> quoted\n\n```\ncode\n```\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n---");
        List<Thickness> spacing = [];

        foreach (Block block in document.Blocks)
        {
            spacing.Add(block.Margin);
            spacing.Add(block.Padding);

            if (block is List list)
            {
                spacing.AddRange(list.ListItems.SelectMany(item => item.Blocks).Select(paragraph => paragraph.Margin));
            }

            if (block is Table table)
            {
                spacing.AddRange(table.RowGroups.SelectMany(group => group.Rows).SelectMany(row => row.Cells).Select(cell => cell.Padding));
            }
        }

        Assert.That(spacing.SelectMany(each => new[] { each.Left, each.Top, each.Right, each.Bottom }),
            Has.All.Matches<double>(value => value % 4 == 0), "D9");
    }

    [Test]
    public void TheStackedCardMark_IsOnTheGrid_AndTakesAThemeColourWhenNoneIsGiven()
    {
        StackedCardMark mark = new();

        Assert.That((mark.Width, mark.Height), Is.EqualTo((20.0, 16.0)), "D9");
        Assert.That(mark.ReadLocalValue(StackedCardMark.ForegroundProperty), Is.EqualTo(DependencyProperty.UnsetValue),
            "no local value, so a template's colour binding wins");
        Assert.That(mark.Children.OfType<System.Windows.Shapes.Rectangle>().Select(outline => outline.Stroke), Has.None.SameAs(Brushes.Gray), "E1: no fixed grey");

        mark.Foreground = Brushes.Teal;

        Assert.That(mark.Children.OfType<System.Windows.Shapes.Rectangle>().Select(outline => outline.Stroke), Has.All.SameAs(Brushes.Teal));
    }

    [Test]
    public void AStateCountChip_IsAPillOnTheGrid()
    {
        StateCountChip chip = new();

        Assert.That((chip.Padding, chip.CornerRadius), Is.EqualTo((new Thickness(12, 4, 12, 4), new CornerRadius(12))));
    }

    [Test]
    public void AGlyphIsInTheRawViewOnly_SoAScreenReaderReadsTheButtonsName()
    {
        AutomationPeer peer = UIElementAutomationPeer.CreatePeerForElement(new GlyphText { Text = "" });

        Assert.That((peer.IsControlElement(), peer.IsContentElement()), Is.EqualTo((false, false)), "W50, E10");
    }

    [TestCase("ToolIcon")]
    [TestCase("ToolButton")]
    [TestCase("ToolIconToggle")]
    public void AToolbarButtonsGlyph_IsDrawnByGlyphText(string key)
    {
        ResourceDictionary shared = LooseXaml.Dictionary("Themes", "Interactive.xaml");
        ContentControl button = key == "ToolIconToggle" ? new System.Windows.Controls.Primitives.ToggleButton() : new Button();
        button.Content = "";
        button.Style = (Style)shared[key];
        System.Windows.Automation.AutomationProperties.SetName(button, "New workstream");
        Window window = new()
        {
            Content = button, Left = -30000, Top = 0, Width = 200, Height = 100, ShowActivated = false, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };

        try
        {
            window.Show();
            window.UpdateLayout();
            List<TextBlock> texts = [.. Descendants(button).OfType<TextBlock>()];

            Assert.That(texts.Select(text => (text.GetType(), text.Text)), Is.EqualTo(new[] { (typeof(GlyphText), "") }));
            Assert.That((UIElementAutomationPeer.CreatePeerForElement(button).GetChildren() ?? []).Where(child => child.IsControlElement() || child.IsContentElement()),
                Is.Empty, "nothing under the button's name in the control or content view");
        }
        finally
        {
            window.Close();
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);

            yield return child;

            foreach (DependencyObject below in Descendants(child))
            {
                yield return below;
            }
        }
    }
}
