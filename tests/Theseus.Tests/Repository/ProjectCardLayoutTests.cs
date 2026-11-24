using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;
using Theseus.App.Controls;
using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Repository;

/// <summary>
/// The project card as MainWindow.xaml draws it (1.1d AC 38, 39, 41, 43, 44), laid out at a cell's width: the template
/// itself, less the window's event handlers, with the few resources it names declared as the window declares them.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ProjectCardLayoutTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string Colour = "#8674D6";

    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private ProjectCardViewModel Theseus(SessionLifecycle git22 = SessionLifecycle.Idle)
    {
        var card = new ProjectCardViewModel(TheseusProject, "Theseus bootstrap") { ColourHex = Colour, PhaseText = "Phase 1: Evidence" };
        ProjectCardFace.Fill(card, TheseusPieces(_folder, git22));

        return card;
    }

    private static Border LaidOut(ProjectCardViewModel card, double width = 280)
    {
        var host = (ContentControl)XamlReader.Parse(HostMarkup());
        host.Content = card;
        host.Width = width;

        host.Measure(new Size(width, 600));
        host.Arrange(new Rect(0, 0, width, host.DesiredSize.Height));
        host.UpdateLayout();

        return Named<Border>(host, "ProjectFace");
    }

    [Test]
    public void AProjectCard_IsUnmistakablyAProject_AStripeTheMarkAndTheWordProject_InItsOwnColour()
    {
        var face = LaidOut(Theseus());
        var stripe = Named<Border>(face, "ProjectStripe");
        var mark = Descendants<StackedCardMark>(face).Single();

        Assert.That(face.CornerRadius, Is.EqualTo(new CornerRadius(8)));
        Assert.That((stripe.ActualWidth, ((SolidColorBrush)stripe.Background).Color.ToString()), Is.EqualTo((5.0, "#FF8674D6")));
        Assert.That(((SolidColorBrush)mark.Foreground).Color.ToString(), Is.EqualTo("#FF8674D6"));
        Assert.That((mark.Width, mark.Height), Is.EqualTo((20.0, 16.0)), "on the 4 epx grid (baseline W36, D9)");
        Assert.That(Named<TextBlock>(face, "ProjectTag").Text, Is.EqualTo("Project"), "a tag in sentence case, not a header (A20, W17)");
        Assert.That(Named<TextBlock>(face, "ProjectTitle").Text, Is.EqualTo("Theseus bootstrap"));
    }

    [Test]
    public void TheAccentBorder_MeansSelected_AsOnACard()
    {
        var card = Theseus();
        var face = LaidOut(card);

        Assert.That(face.BorderThickness, Is.EqualTo(new Thickness(1)), "no permanent accent border");

        card.IsSelected = true;
        face.UpdateLayout();

        Assert.That(face.BorderThickness, Is.EqualTo(new Thickness(2)));
    }

    [Test]
    public void WhereItIs_TheSentenceOnOneLine_TrimmedWithItWholeInTheTooltip_ThenTheChipsThatAreNotZero()
    {
        var card = Theseus();
        var face = LaidOut(card);
        var sentence = Named<TextBlock>(face, "ProjectSentence");
        var chips = Descendants<StateCountChip>(Named<ItemsControl>(face, "ProjectCounts")).Select(chip => ((TextBlock)chip.Child).Text);

        Assert.That((sentence.Text, sentence.ToolTip), Is.EqualTo((card.Sentence, (object)card.Sentence)));
        Assert.That((sentence.TextWrapping, sentence.TextTrimming), Is.EqualTo((TextWrapping.NoWrap, TextTrimming.CharacterEllipsis)));
        Assert.That(sentence.ActualWidth, Is.LessThanOrEqualTo(face.ActualWidth), "trimmed, never past the card");
        Assert.That(chips, Is.EqualTo(new[] { "✓ 7 done", "◆ 3 waiting on you", "– 2 to do" }));
    }

    [Test]
    public void StartAndStop_AreTheCardsOwnIconButtons_WithTheirReasonsAsTooltips()
    {
        var card = Theseus(SessionLifecycle.Running);
        var face = LaidOut(card);
        var start = Named<Button>(face, "ProjectStart");
        var stop = Named<Button>(face, "ProjectStop");

        Assert.That(Named<StackPanel>(face, "ProjectButtons").Visibility, Is.EqualTo(Visibility.Visible));
        Assert.That((start.ToolTip, stop.ToolTip), Is.EqualTo(((object)card.StartToolTip, (object)card.StopToolTip)));
        Assert.That(start.ToolTip, Is.EqualTo("No card of Theseus bootstrap can start: git-22 is already running"));
    }

    [Test]
    public void ACardThatNeedsUpdating_SaysOnlyThat_KeepsItsMarkAndColour_AndOffersUpdateNotOpenStartOrStop()
    {
        var card = new ProjectCardViewModel("bym", "bym")
        {
            IsDimmed = true,
            ColourHex = Colour,
            PhaseText = "Needs updating",
            Problem = "Pinned to method-v0.1: its project.md is in the older shape",
        };
        var face = LaidOut(card);

        Assert.That(face.Opacity, Is.EqualTo(0.55));
        Assert.That(Named<Button>(face, "ProjectOpen").Visibility, Is.EqualTo(Visibility.Collapsed), "its ↗ becomes Update (1.1e AC 18)");
        Assert.That((Named<Button>(face, "ProjectUpdate").Visibility, Named<Button>(face, "ProjectUpdate").Content), Is.EqualTo((Visibility.Visible, (object)"Update")));
        Assert.That((Named<Button>(face, "ProjectStart").Visibility, Named<Button>(face, "ProjectStop").Visibility),
            Is.EqualTo((Visibility.Collapsed, Visibility.Collapsed)));
        Assert.That(Named<TextBlock>(face, "ProjectSentence").Visibility, Is.EqualTo(Visibility.Collapsed));
        Assert.That(Named<TextBlock>(face, "ProjectTag").Text, Is.EqualTo("Project"));
        Assert.That(((SolidColorBrush)Descendants<StackedCardMark>(face).Single().Foreground).Color.ToString(), Is.EqualTo("#FF8674D6"));
        Assert.That(Descendants<TextBlock>(face).Select(text => text.Text), Does.Contain("Needs updating").And.No.Contain(card.Problem),
            "no reason on its face");
        Assert.That(face.ToolTip, Is.EqualTo($"bym{Environment.NewLine}{card.Problem}"), "the reason is in its tooltip");
    }

    [Test]
    public void Open_IsTheFirstButton_BeforeStartAndStop_AndOpensTheProject()
    {
        var opened = 0;
        var card = Theseus();
        card.OpenCommand = new RelayCommand(_ => opened++);
        var face = LaidOut(card);
        var buttons = Descendants<Button>(Named<StackPanel>(face, "ProjectButtons")).Select(button => button.Name);
        var open = Named<Button>(face, "ProjectOpen");

        Assert.That(buttons, Is.EqualTo(new[] { "ProjectOpen", "ProjectUpdate", "ProjectFocusLead", "ProjectStart", "ProjectStop" }),
            "Update stands in for ↗ only on a card that needs updating (1.1e AC 18)");
        Assert.That(Named<Button>(face, "ProjectUpdate").Visibility, Is.EqualTo(Visibility.Collapsed));
        Assert.That((open.ToolTip, open.Content), Is.EqualTo(((object)"Open the project (Enter)", (object)"")));

        open.Command.Execute(null);

        Assert.That(opened, Is.EqualTo(1));
    }

    [Test]
    public void ALongTitle_IsTrimmedWithATooltip_AndPushesNothingOffTheCard()
    {
        var card = new ProjectCardViewModel("long", "A project whose title is a great deal longer than any cell is wide") { ColourHex = Colour };
        var face = LaidOut(card, 240);
        var title = Named<TextBlock>(face, "ProjectTitle");
        var tag = Named<TextBlock>(face, "ProjectTag");

        Assert.That((title.TextTrimming, title.ToolTip), Is.EqualTo((TextTrimming.CharacterEllipsis, (object)card.Title)));
        Assert.That(tag.TranslatePoint(new Point(tag.ActualWidth, 0), face).X, Is.LessThanOrEqualTo(face.ActualWidth));
    }

    private static string HostMarkup()
    {
        var window = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "MainWindow.xaml"));
        var template = new XElement(window.Descendants(Presentation + "DataTemplate")
            .Single(element => (string?)element.Attribute(Xaml + "Key") == "ProjectCardTemplate"));

        template.Attribute(Xaml + "Key")!.Remove();

        // The window's handlers need the window behind them; what they do is tested apart (BoardGestureTests).
        foreach (var handled in template.Descendants().Attributes()
                     .Where(attribute => attribute.Name.LocalName is "MouseLeftButtonDown" or "MouseLeftButtonUp" or "PreviewMouseRightButtonDown" or "Opened")
                     .ToList())
        {
            handled.Remove();
        }

        var host = new XElement(Presentation + "ContentControl",
            new XAttribute(XNamespace.Xmlns + "x", Xaml.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "vm", "clr-namespace:Theseus.App.ViewModels;assembly=Theseus"),
            new XAttribute(XNamespace.Xmlns + "controls", "clr-namespace:Theseus.App.Controls;assembly=Theseus"),
            new XElement(Presentation + "ContentControl.Resources",
                Converter("HexToBrushConverter", "HexToBrush"),
                Converter("BoolToVisibilityConverter", "BoolToVisibility"),
                Converter("NotBoolToVisibilityConverter", "NotBoolToVisibility"),
                Converter("NotEmptyToVisibilityConverter", "NotEmptyToVisibility"),
                Style("MenuGlyph", "TextBlock"),
                Style("CardIcon", "Button"),
                Style("CommandButton", "Button"),
                Style("ClickableCard", "Border"),
                Style("TagChipButton", "Button"),
                Style("TagMenuItem", "MenuItem")),
            new XElement(Presentation + "ContentControl.ContentTemplate", template));

        // The window's own namespaces name no assembly, which a parsed fragment needs.
        foreach (var element in host.DescendantsAndSelf().Where(element => element.Name.NamespaceName.StartsWith("clr-namespace:Theseus.App.", StringComparison.Ordinal)
                     && !element.Name.NamespaceName.Contains(";assembly=", StringComparison.Ordinal)))
        {
            element.Name = XName.Get(element.Name.LocalName, element.Name.NamespaceName + ";assembly=Theseus");
        }

        // So are attached properties, such as the search's highlighted title (card-search-and-tags item 3).
        foreach (var attribute in host.DescendantsAndSelf().Attributes().Where(attribute =>
                     attribute.Name.NamespaceName.StartsWith("clr-namespace:Theseus.App.", StringComparison.Ordinal)
                     && !attribute.Name.NamespaceName.Contains(";assembly=", StringComparison.Ordinal)).ToList())
        {
            var parent = attribute.Parent!;
            attribute.Remove();
            parent.Add(new XAttribute(XName.Get(attribute.Name.LocalName, attribute.Name.NamespaceName + ";assembly=Theseus"), attribute.Value));
        }

        return host.ToString();
    }

    private static XElement Converter(string type, string key) =>
        new(XName.Get(type, "clr-namespace:Theseus.App.Converters;assembly=Theseus"), new XAttribute(Xaml + "Key", key));

    private static XElement Style(string key, string target) =>
        new(Presentation + "Style", new XAttribute(Xaml + "Key", key), new XAttribute("TargetType", target));

    private static T Named<T>(DependencyObject root, string name) where T : FrameworkElement =>
        Descendants<T>(root).First(element => element.Name == name);

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
}
