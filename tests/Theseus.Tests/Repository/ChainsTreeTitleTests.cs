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
/// A title too long for the Chains panel gives way, not the state beside it: the title ends in "…" and the state
/// still shows in full, inside the panel (links-tree-scrollbar rule 3 and 4, Nigel 2026-11-19). Laid out as the board
/// lays it out, under the Fluent theme (P11).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ChainsTreeTitleTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const double PanelContentWidth = 318;
    private const string LongTitle = "A very long workstream title that is a good deal wider than the chains panel ever is";

    [Test]
    public void ALongTitle_IsTrimmedWithinThePanel()
    {
        var tree = LaidOut();
        var title = Descendants<TextBlock>(tree).First(text => text.Text == LongTitle);

        Assert.That(title.TextTrimming, Is.EqualTo(TextTrimming.CharacterEllipsis));
        Assert.That(title.TranslatePoint(new Point(title.ActualWidth, 0), tree).X,
            Is.LessThanOrEqualTo(PanelContentWidth + 0.5), "the title ends inside the panel");
    }

    [Test]
    public void ALongTitle_LeavesTheStateShowingInsideThePanel()
    {
        var tree = LaidOut();
        var state = Descendants<TextBlock>(tree).First(text => text.Text == "waiting");
        var left = state.TranslatePoint(new Point(0, 0), tree).X;

        Assert.That(state.ActualWidth, Is.GreaterThan(0));
        Assert.That(left + state.ActualWidth, Is.LessThanOrEqualTo(PanelContentWidth + 0.5),
            "the state is not pushed off the panel's edge");
    }

    private static TreeView LaidOut()
    {
        var node = new ChainNodeViewModel("long", LongTitle, "#C0392B", "waiting", IsReady: false,
            string.Empty, string.Empty, new RelayCommand(_ => { }));

        var host = (Border)XamlReader.Parse(PanelMarkup());
        host.DataContext = new Board(new Chains(new ObservableCollection<ChainNodeViewModel>([node])));
        host.Width = PanelContentWidth;
        host.Height = 600;
        host.Measure(new Size(PanelContentWidth, 600));
        host.Arrange(new Rect(0, 0, PanelContentWidth, 600));
        host.UpdateLayout();

        return Descendants<TreeView>(host).Single();
    }

    private static string PanelMarkup()
    {
        var window = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "MainWindow.xaml"));
        var tree = window.Descendants(Presentation + "TreeView")
            .Single(element => (string?)element.Attribute("ItemsSource") == "{Binding Chains.Roots}");
        var panelScroller = tree.Ancestors(Presentation + "ScrollViewer").First();
        const string converters = "clr-namespace:Theseus.App.Converters;assembly=Theseus";

        return new XElement(Presentation + "Border",
            new XAttribute(XNamespace.Xmlns + "converters", converters),
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            new XElement(Presentation + "Border.Resources",
                new XElement(Presentation + "ResourceDictionary",
                    new XElement(Presentation + "ResourceDictionary.MergedDictionaries",
                        new XElement(Presentation + "ResourceDictionary",
                            new XAttribute("Source", "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml"))),
                    Converter(converters, "HexToBrushConverter", "HexToBrush"),
                    Converter(converters, "CountToVisibilityConverter", "CountToVisibility"),
                    Converter(converters, "NotEmptyToVisibilityConverter", "NotEmptyToVisibility"))),
            new XElement(Presentation + "ScrollViewer",
                panelScroller.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration),
                new XElement(Presentation + "StackPanel", new XElement(tree)))).ToString();
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
