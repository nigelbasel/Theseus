using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Theseus.App.Behaviors;
using Theseus.App.Controls;
using Theseus.App.Converters;
using Theseus.App.Text;
using Theseus.App.Views;
using Theseus.Tests.Projects;
using Theseus.Tests.ViewModels;

namespace Theseus.Tests.Conventions;

/// <summary>
/// The shared behaviours and helpers of Windows conventions batch WB1: the access-key escape (W26; B12, C-m3), the
/// trimmed-text tooltip (W35; C20), the Markdown box's selector bar and label (W15, W50; D7, E12) and a link's
/// accessible name (W50; E15).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class SharedBehaviourTests
{
    [TestCase("a_b", "a__b")]
    [TestCase("my_card_2", "my__card__2")]
    [TestCase("plain", "plain")]
    [TestCase(null, "")]
    public void AccessKeyText_DoublesEveryUnderscore(string? text, string escaped)
    {
        Assert.That(AccessKeyText.Escape(text), Is.EqualTo(escaped));
    }

    [Test]
    public void AnEscapedName_KeepsItsUnderscoreWhenDrawnAsAnAccessKeyLabel()
    {
        AccessText label = new() { Text = AccessKeyText.Escape("wg_shared") };
        TextBlock drawn = Descendants(label).OfType<TextBlock>().Single();

        Assert.That((label.AccessKey, new TextRange(drawn.ContentStart, drawn.ContentEnd).Text), Is.EqualTo(((char)0, "wg_shared")));
    }

    [Test]
    public void TheEscapeConverter_IsRegisteredInAppXaml_AndEscapes()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement registration = XDocument.Load(LooseXaml.SourceFile("App.xaml")).Descendants()
            .Single(element => (string?)element.Attribute(xaml + "Key") == "AccessKeyEscape");

        Assert.That(registration.Name.LocalName, Is.EqualTo(nameof(AccessKeyEscapeConverter)));
        Assert.That(new AccessKeyEscapeConverter().Convert("x_y", typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo("x__y"));
    }

    [Test]
    public void AMarkdownBoxGivenALabel_NamesItsEditorFromThatLabel()
    {
        TextBlock label = new() { Text = "Opening prompt" };
        MarkdownBox box = new();

        Assert.That(AutomationProperties.GetName(box.Editor), Is.EqualTo("Markdown text"), "with no label");

        AutomationProperties.SetLabeledBy(box, label);

        Assert.That((AutomationProperties.GetLabeledBy(box.Editor), AutomationProperties.GetName(box.Editor)), Is.EqualTo(((UIElement)label, string.Empty)));

        MarkdownBox named = new();
        AutomationProperties.SetName(named, "Acceptance criteria");

        Assert.That(AutomationProperties.GetName(named.Editor), Is.EqualTo("Acceptance criteria"));
    }

    [Test]
    public void AMarkdownBoxsWriteAndPreview_AreASelectorBar_OneTabStopWithTheArrowsInside()
    {
        MarkdownBox box = new();
        List<RadioButton> items = [.. Descendants(box).OfType<RadioButton>()];
        Panel bar = (Panel)items[0].Parent;

        Assert.That(items.Select(item => item.Content), Is.EqualTo(new[] { "Write", "Preview" }));
        Assert.That(items.Select(item => item.GroupName).Distinct().Count(), Is.EqualTo(1));
        Assert.That(items.Select(SelectorBar.GetSelectsOnFocus), Has.All.True);
        Assert.That((KeyboardNavigation.GetTabNavigation(bar), KeyboardNavigation.GetDirectionalNavigation(bar)),
            Is.EqualTo((KeyboardNavigationMode.Once, KeyboardNavigationMode.Contained)), "W42");
        Assert.That(Descendants(box).OfType<TextBlock>().Select(text => text.Text), Has.None.EqualTo("|"), "no hand-drawn separator");

        box.IsPreviewing = true;

        Assert.That(items.Select(item => item.IsChecked), Is.EqualTo(new bool?[] { false, true }));

        items[0].IsChecked = true;

        Assert.That(box.IsPreviewing, Is.False);
    }

    [Test]
    public void ATrimmedToolTip_OpensOnlyWhenTheTextIsCutShort()
    {
        TextBlock longLine = new() { Text = "A title far too long to fit in the narrow column it has", TextTrimming = TextTrimming.CharacterEllipsis, Width = 80 };
        TextBlock shortLine = new() { Text = "Short", TextTrimming = TextTrimming.CharacterEllipsis, Width = 200 };
        StackPanel panel = new() { Children = { longLine, shortLine } };
        TrimmedToolTip.SetIsEnabled(longLine, true);
        TrimmedToolTip.SetIsEnabled(shortLine, true);
        panel.Measure(new Size(400, 200));
        panel.Arrange(new Rect(0, 0, 400, 200));

        Assert.That((TrimmedToolTip.IsTrimmed(longLine), TrimmedToolTip.IsTrimmed(shortLine)), Is.EqualTo((true, false)));
        Assert.That((longLine.ToolTip, shortLine.ToolTip), Is.EqualTo(((object)longLine.Text, (object)shortLine.Text)), "the text is the tooltip");
        Assert.That(Opens(longLine), Is.True);
        Assert.That(Opens(shortLine), Is.False, "W35: no tooltip that repeats what is on the screen");
    }

    [Test]
    public void ALinkInAProjectViewSentence_IsNamedByItsText_WithWhereItGoesAsHelpText()
    {
        using TestProjectsFolder folder = TestProjectsFolder.Create();
        Theseus.App.ViewModels.ProjectViewModel view = new ProjectViews(folder).Theseus();
        TextBlock sentence = new();
        ProjectView control = new() { DataContext = view };
        control.Content = sentence;
        LinkedText.SetText(sentence, "Objective 0.2 waits on 0.1.");
        Window window = new()
        {
            Content = control, Left = -30000, Top = 0, Width = 600, Height = 200, ShowActivated = false, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };

        try
        {
            window.Show();
            window.UpdateLayout();
            List<Hyperlink> links = [.. sentence.Inlines.OfType<Hyperlink>()];

            Assert.That(links, Is.Not.Empty, "the fixture's objectives 0.1 and 0.2 are linked");
            Assert.That(links.Select(link => AutomationProperties.GetName(link)), Is.EqualTo(links.Select(TextOf)), "E15: the name is what is on the screen");
            Assert.That(links.Select(link => AutomationProperties.GetHelpText(link)), Is.EqualTo(links.Select(link => link.ToolTip as string)));
            Assert.That(links.Select(link => link.ToolTip as string), Has.All.Not.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    private static bool Opens(TextBlock block)
    {
        ToolTipEventArgs opening = (ToolTipEventArgs)typeof(ToolTipEventArgs)
            .GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, [typeof(bool)])!
            .Invoke([true]);
        opening.RoutedEvent = FrameworkElement.ToolTipOpeningEvent;
        block.RaiseEvent(opening);

        return !opening.Handled;
    }

    private static string TextOf(Hyperlink link) => string.Concat(link.Inlines.OfType<Run>().Select(run => run.Text));

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        IEnumerable<DependencyObject> children = root is Visual
            ? Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(root)).Select(index => VisualTreeHelper.GetChild(root, index))
            : [];

        foreach (DependencyObject child in children.Concat(LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()).Distinct())
        {
            yield return child;

            foreach (DependencyObject below in Descendants(child))
            {
                yield return below;
            }
        }
    }
}
