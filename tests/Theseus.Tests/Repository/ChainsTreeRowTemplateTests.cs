using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using Theseus.App.ViewModels;

namespace Theseus.Tests.Repository;

/// <summary>
/// What the Chains tree's own row template keeps from the standard one: a chevron for rows with children only,
/// collapsing that hides the children, and children indented under their parent (links-tree-scrollbar, overnight
/// decision 2; verify should-fix 1). Laid out under the Fluent theme, as the board does (P11).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ChainsTreeRowTemplateTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const double PanelContentWidth = 318;

    [Test]
    public void ARowWithChildren_ShowsAChevron()
    {
        var (tree, _) = LaidOut();

        Assert.That(Chevron(Row(tree, "Parent")).Visibility, Is.EqualTo(Visibility.Visible));
    }

    [Test]
    public void ARowWithoutChildren_HidesItsChevron_KeepingItsPlace()
    {
        var (tree, _) = LaidOut();

        Assert.That(Chevron(Row(tree, "Child")).Visibility, Is.EqualTo(Visibility.Hidden),
            "hidden, not collapsed, so titles stay aligned");
    }

    [Test]
    public void CollapsingARow_HidesItsChildren()
    {
        var (tree, host) = LaidOut();

        Chevron(Row(tree, "Parent")).IsChecked = false;
        host.UpdateLayout();

        Assert.That(Descendants<ItemsPresenter>(Row(tree, "Parent")).First().Visibility, Is.EqualTo(Visibility.Collapsed),
            "the chevron sets IsExpanded, and a collapsed row's children are not laid out");
    }

    [Test]
    public void AChild_IsIndentedUnderItsParent()
    {
        var (tree, _) = LaidOut();
        var parent = Row(tree, "Parent").TranslatePoint(new Point(0, 0), tree).X;
        var child = Row(tree, "Child").TranslatePoint(new Point(0, 0), tree).X;

        Assert.That(child - parent, Is.EqualTo(16).Within(0.5));
    }

    private static TreeViewItem Row(TreeView tree, string title) =>
        Descendants<TreeViewItem>(tree).First(item => ((ChainNodeViewModel)item.DataContext).Title == title);

    private static ToggleButton Chevron(TreeViewItem row) =>
        Descendants<ToggleButton>(row).First();

    private static (TreeView Tree, Border Host) LaidOut()
    {
        var parent = Node("Parent");
        parent.Children.Add(Node("Child"));

        var host = (Border)XamlReader.Parse(PanelMarkup());
        host.DataContext = new Board(new Chains(new ObservableCollection<ChainNodeViewModel>([parent])));
        host.Width = PanelContentWidth;
        host.Height = 600;
        host.Measure(new Size(PanelContentWidth, 600));
        host.Arrange(new Rect(0, 0, PanelContentWidth, 600));
        host.UpdateLayout();

        return (Descendants<TreeView>(host).Single(), host);
    }

    private static ChainNodeViewModel Node(string title) =>
        new(title, title, "#C0392B", "waiting", IsReady: false, string.Empty, string.Empty, new RelayCommand(_ => { }));

    private static string PanelMarkup()
    {
        var window = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "MainWindow.xaml"));
        var tree = window.Descendants(Presentation + "TreeView")
            .Single(element => (string?)element.Attribute("ItemsSource") == "{Binding Chains.Roots}");
        var panelScroller = tree.Ancestors(Presentation + "ScrollViewer").First();
        const string converters = "clr-namespace:Theseus.App.Converters;assembly=Theseus";

        return new XElement(Presentation + "Border",
            new XAttribute(XNamespace.Xmlns + "converters", converters),
            new XAttribute(XNamespace.Xmlns + "controls", "clr-namespace:Theseus.App.Controls"),
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
                new XElement(Presentation + "StackPanel", new XElement(tree)))).ToString()
            .Replace("\"clr-namespace:Theseus.App.Controls\"", "\"clr-namespace:Theseus.App.Controls;assembly=Theseus\"", StringComparison.Ordinal);
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
