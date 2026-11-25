using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using Theseus.App.ViewModels;

namespace Theseus.Tests.Repository;

/// <summary>
/// What the Chains tree's own row template must not lose for someone using the keyboard or a screen reader
/// (links-tree-scrollbar review should-fix 1 and 2): the row the arrow keys are on shows, and the title button still
/// says what pressing it does. Laid out under the Fluent theme, as the board does (P11).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ChainsTreeAccessibilityTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const double PanelContentWidth = 318;
    private const string AppControls = "clr-namespace:Theseus.App.Controls";

    [Test]
    public void TheSelectedRow_IsHighlighted()
    {
        var (tree, host) = LaidOut();
        var row = Descendants<TreeViewItem>(tree).First();

        row.IsSelected = true;
        host.UpdateLayout();

        var highlight = Descendants<Border>(row).First(border => border.Name == "Highlight");
        TestContext.Out.WriteLine($"selected {row.IsSelected}; highlight {highlight.Background}");
        Assert.That(highlight.Background, Is.Not.Null.And.Not.EqualTo(Brushes.Transparent),
            "the arrow keys move the selection; without a highlight the keyboard user cannot see where they are");
    }

    [Test]
    public void TheSelectedRow_CarriesTheAccentSelectionBar()
    {
        var (tree, host) = LaidOut();
        var row = Descendants<TreeViewItem>(tree).First();
        var bar = Descendants<Border>(row).First(border => border.Name == "SelectionBar");

        Assert.That(bar.Visibility, Is.EqualTo(Visibility.Collapsed), "no mark before it is selected");

        row.IsSelected = true;
        host.UpdateLayout();

        Assert.That(bar.Visibility, Is.EqualTo(Visibility.Visible),
            "the fill alone is a contrast of about 1.1:1; Fluent marks a selected row with an accent bar (re-verify should-fix 1)");
        Assert.That(bar.ActualWidth, Is.EqualTo(3).Within(0.5));
    }

    [Test]
    public void AnUnselectedRow_IsNotHighlighted()
    {
        var (tree, _) = LaidOut();
        var highlight = Descendants<Border>(Descendants<TreeViewItem>(tree).First()).First(border => border.Name == "Highlight");

        Assert.That(highlight.Background is null || highlight.Background == Brushes.Transparent, Is.True);
    }

    [Test]
    public void TheTitleButton_TellsAScreenReaderWhatItDoes()
    {
        var (tree, _) = LaidOut();
        var button = Descendants<Button>(tree).First(each => each.Content is TextBlock);

        Assert.That(UIElementAutomationPeer.CreatePeerForElement(button).GetHelpText(), Is.EqualTo("Open this card's details"),
            "a tooltip that is not a string gives a screen reader nothing");
    }

    [Test]
    public void ARow_IsNamedByItsTitle_NotByItsRecord()
    {
        // A record's ToString read "ChainNodeViewModel { Key = one, ... }" to a screen reader (E11, W50, G170).
        var (tree, _) = LaidOut();
        string name = UIElementAutomationPeer.CreatePeerForElement(Descendants<TreeViewItem>(tree).First()).GetName();

        Assert.That(name, Is.EqualTo("One, stopped"), "its title and its state's word (chains-panel-at-scale criterion 12)");
        Assert.That(name, Does.Not.Contain("Theseus.").And.Not.Contain(" { "));
        Assert.That(System.Windows.Automation.AutomationProperties.GetName(tree), Is.EqualTo("Chains"));
    }

    private static (TreeView Tree, Border Host) LaidOut()
    {
        var node = new ChainNodeViewModel("one", "One", "#C0392B", "waiting", IsReady: false, string.Empty, string.Empty,
            new RelayCommand(_ => { }));

        var host = (Border)XamlReader.Parse(PanelMarkup());
        host.DataContext = new Board(new Chains(new ObservableCollection<ChainNodeViewModel>([node])));
        host.Width = PanelContentWidth;
        host.Height = 600;
        host.Measure(new Size(PanelContentWidth, 600));
        host.Arrange(new Rect(0, 0, PanelContentWidth, 600));
        host.UpdateLayout();

        return (Descendants<TreeView>(host).Single(), host);
    }

    private static string PanelMarkup()
    {
        var window = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "MainWindow.xaml"));
        var tree = window.Descendants(Presentation + "TreeView")
            .Single(element => (string?)element.Attribute("ItemsSource") == "{Binding Chains.Roots}");
        tree.Attribute("PreviewMouseWheel")?.Remove(); // a code-behind handler, which a parsed fragment cannot hold
        const string converters = "clr-namespace:Theseus.App.Converters;assembly=Theseus";

        return new XElement(Presentation + "Border",
            new XAttribute(XNamespace.Xmlns + "converters", converters),
            new XAttribute(XNamespace.Xmlns + "controls", AppControls),
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            new XElement(Presentation + "Border.Resources",
                new XElement(Presentation + "ResourceDictionary",
                    new XElement(Presentation + "ResourceDictionary.MergedDictionaries",
                        new XElement(Presentation + "ResourceDictionary",
                            new XAttribute("Source", "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml")),
                        new XElement(Presentation + "ResourceDictionary",
                            new XAttribute("Source", "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.Light.xaml"))),
                    Converter(converters, "HexToBrushConverter", "HexToBrush"),
                    Converter(converters, "CountToVisibilityConverter", "CountToVisibility"),
                    Converter(converters, "NotEmptyToVisibilityConverter", "NotEmptyToVisibility"))),
            new XElement(Presentation + "Grid", new XElement(tree))).ToString()
            .Replace($"\"{AppControls}\"", $"\"{AppControls};assembly=Theseus\"", StringComparison.Ordinal);
    }

    private static XElement Converter(string ns, string type, string key) =>
        new(XName.Get(type, ns), new XAttribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"), key));

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (var deeper in Descendants<T>(child))
            {
                yield return deeper;
            }
        }
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));

    private sealed record Board(Chains Chains);

    private sealed record Chains(ObservableCollection<ChainNodeViewModel> Roots);
}
