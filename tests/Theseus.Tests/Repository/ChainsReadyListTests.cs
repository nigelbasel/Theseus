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
/// The Chains panel's "Ready" list treats a long title as the tree does: it ends in "…" inside the panel, and the
/// button still tells a screen reader what it does (links-tree-scrollbar question 3, Nigel 2026-11-19: option A).
/// Laid out under the Fluent theme, as the board does (P11).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ChainsReadyListTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const double PanelContentWidth = 318;
    private const string LongTitle = "A very long workstream title that is a good deal wider than the chains panel ever is";

    [Test]
    public void ALongTitle_EndsInAnEllipsisInsideThePanel()
    {
        var list = LaidOut();
        var title = Descendants<TextBlock>(list).First(text => text.Text == LongTitle);

        Assert.That(title.TextTrimming, Is.EqualTo(TextTrimming.CharacterEllipsis));
        Assert.That(title.TranslatePoint(new Point(title.ActualWidth, 0), list).X,
            Is.LessThanOrEqualTo(PanelContentWidth + 0.5), "the title ends inside the panel, not past its edge");
    }

    [Test]
    public void TheTitleButton_TellsAScreenReaderWhatItDoes()
    {
        var button = Descendants<Button>(LaidOut()).First();

        Assert.That(UIElementAutomationPeer.CreatePeerForElement(button).GetHelpText(), Is.EqualTo("Open this card's details"));
    }

    private static ItemsControl LaidOut()
    {
        var node = new ChainNodeViewModel("long", LongTitle, "#C0392B", "Ready", IsReady: true, "waits on nothing",
            string.Empty, new RelayCommand(_ => { }));

        var host = (Border)XamlReader.Parse(PanelMarkup());
        host.DataContext = new Board(new Chains(new ObservableCollection<ChainNodeViewModel>([node])));
        host.Width = PanelContentWidth;
        host.Height = 600;
        host.Measure(new Size(PanelContentWidth, 600));
        host.Arrange(new Rect(0, 0, PanelContentWidth, 600));
        host.UpdateLayout();

        return Descendants<ItemsControl>(host).Single();
    }

    // The Ready list exactly as MainWindow.xaml has it, inside a scroll viewer with the panel's attributes.
    private static string PanelMarkup()
    {
        var window = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "MainWindow.xaml"));
        var list = window.Descendants(Presentation + "ItemsControl")
            .Single(element => (string?)element.Attribute("ItemsSource") == "{Binding Chains.Ready}");
        var panelScroller = list.Ancestors(Presentation + "ScrollViewer").First();
        const string converters = "clr-namespace:Theseus.App.Converters;assembly=Theseus";

        return new XElement(Presentation + "Border",
            new XAttribute(XNamespace.Xmlns + "converters", converters),
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            new XElement(Presentation + "Border.Resources",
                new XElement(Presentation + "ResourceDictionary",
                    new XElement(Presentation + "ResourceDictionary.MergedDictionaries",
                        new XElement(Presentation + "ResourceDictionary",
                            new XAttribute("Source", "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml")),
                        new XElement(Presentation + "ResourceDictionary",
                            new XAttribute("Source", "pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.Light.xaml"))),
                    Converter(converters, "HexToBrushConverter", "HexToBrush"))),
            new XElement(Presentation + "ScrollViewer",
                panelScroller.Attributes().Where(attribute => !attribute.IsNamespaceDeclaration && attribute.Name.LocalName != "PreviewMouseWheel"),
                new XElement(Presentation + "StackPanel", new XElement(list)))).ToString();
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

    private sealed record Chains(ObservableCollection<ChainNodeViewModel> Ready);
}
