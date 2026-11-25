using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using Theseus.App.ViewModels;

namespace Theseus.Tests.Repository;

/// <summary>
/// The Chains panel's tree, laid out as the board lays it out: the tree's own markup from MainWindow.xaml, inside the
/// panel's vertical scroll viewer, at the panel's width. A title wider than the panel made the tree's own horizontal
/// scrollbar appear inside the tree's height and cover its bottom row (Nigel, 2026-11-19). Checked against what is laid
/// out, not against the markup (method rule P11).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ChainsTreeLayoutTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const double PanelContentWidth = 318;

    [Test]
    public void ALongTitle_DoesNotCoverTheTreesBottomRow()
    {
        var tree = LaidOut(
            Node("A very long workstream title that is a good deal wider than the chains panel ever is", Node("Its follow-on")),
            Node("Another chain"));

        var lastRow = Descendants<TreeViewItem>(tree).Last();
        var rowBottom = lastRow.TranslatePoint(new Point(0, lastRow.ActualHeight), tree).Y;

        var scroller = Descendants<ScrollViewer>(tree).First();
        TestContext.Out.WriteLine(
            $"tree {tree.ActualWidth:0.#}x{tree.ActualHeight:0.#}; viewport {scroller.ViewportWidth:0.#}x{scroller.ViewportHeight:0.#}; "
            + $"extent {scroller.ExtentWidth:0.#}x{scroller.ExtentHeight:0.#}; horizontal bar {scroller.ComputedHorizontalScrollBarVisibility}; "
            + $"style {scroller.Style?.TargetType?.Name ?? "default"}; last row ends at {rowBottom:0.#}");

        foreach (var bar in Descendants<System.Windows.Controls.Primitives.ScrollBar>(tree))
        {
            TestContext.Out.WriteLine(
                $"  bar {bar.Orientation} {bar.Visibility} {bar.ActualWidth:0.#}x{bar.ActualHeight:0.#} at y {bar.TranslatePoint(new Point(0, 0), tree).Y:0.#}");
        }

        var covering = Descendants<System.Windows.Controls.Primitives.ScrollBar>(tree)
            .Where(bar => bar.Orientation == Orientation.Horizontal && bar.Visibility == Visibility.Visible && bar.ActualHeight > 0)
            .Select(bar => bar.TranslatePoint(new Point(0, 0), tree).Y)
            .Where(barTop => barTop < rowBottom - 0.5)
            .ToList();

        Assert.That(covering, Is.Empty,
            $"a horizontal scrollbar is drawn over the bottom row (row ends at {rowBottom:0.#})");
    }

    private static TreeView LaidOut(params ChainNodeViewModel[] roots)
    {
        var host = (Border)XamlReader.Parse(PanelMarkup());
        host.DataContext = new Board(new Chains(new ObservableCollection<ChainNodeViewModel>(roots)));
        host.Width = PanelContentWidth;
        host.Height = 600;

        host.Measure(new Size(PanelContentWidth, 600));
        host.Arrange(new Rect(0, 0, PanelContentWidth, 600));
        host.UpdateLayout();

        return Descendants<TreeView>(host).Single();
    }

    // The tree element exactly as MainWindow.xaml has it, inside a scroll viewer with the same attributes as the
    // panel's, and the converters it names declared as App.xaml declares them.
    private static string PanelMarkup()
    {
        var window = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "MainWindow.xaml"));
        var tree = window.Descendants(Presentation + "TreeView")
            .Single(element => (string?)element.Attribute("ItemsSource") == "{Binding Chains.Roots}");
        var panelScroller = tree.Ancestors(Presentation + "ScrollViewer").First();

        // The board runs under WPF's Fluent theme (ThemeApplier sets ThemeMode), whose scroll viewers lay their bars
        // over the content; the classic template gives the bar its own row, which is why the bug does not show
        // without it.
        var host = new XElement(Presentation + "Border",
            new XAttribute(XNamespace.Xmlns + "converters", "clr-namespace:Theseus.App.Converters;assembly=Theseus"),
            new XAttribute(XNamespace.Xmlns + "controls", "clr-namespace:Theseus.App.Controls"),
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            new XElement(Presentation + "Border.Resources",
                new XElement(Presentation + "ResourceDictionary",
                    new XElement(Presentation + "ResourceDictionary.MergedDictionaries",
                        new XElement(Presentation + "ResourceDictionary",
                            new XAttribute("Source", "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml"))),
                    Converter("HexToBrushConverter", "HexToBrush"),
                    Converter("CountToVisibilityConverter", "CountToVisibility"),
                    Converter("NotEmptyToVisibilityConverter", "NotEmptyToVisibility"))),
            new XElement(Presentation + "ScrollViewer",
                panelScroller.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration && attribute.Name.LocalName != "PreviewMouseWheel"),
                new XElement(Presentation + "StackPanel", new XElement(tree))));

        // The app's controls namespace on a prefix, then given its assembly, as a parsed fragment needs.
        return host.ToString().Replace("\"clr-namespace:Theseus.App.Controls\"", "\"clr-namespace:Theseus.App.Controls;assembly=Theseus\"", StringComparison.Ordinal);
    }

    private static XElement Converter(string type, string key) =>
        new(XName.Get(type, "clr-namespace:Theseus.App.Converters;assembly=Theseus"),
            new XAttribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"), key));

    private static ChainNodeViewModel Node(string title, params ChainNodeViewModel[] children)
    {
        var node = new ChainNodeViewModel(title.Replace(' ', '-'), title, "#C0392B", "Ready", IsReady: true,
            string.Empty, string.Empty, new RelayCommand(_ => { }));

        foreach (var child in children)
        {
            node.Children.Add(child);
        }

        return node;
    }

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
