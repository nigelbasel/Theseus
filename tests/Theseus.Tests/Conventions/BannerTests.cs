using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;

namespace Theseus.Tests.Conventions;

/// <summary>
/// Every banner's text can be selected and copied (Nigel, 2026-11-27, 1.3e): each banner is marked, draws its text with the
/// shared SelectableText style and has a Copy button that puts its whole text on the clipboard. The markup tests are a
/// ratchet: a banner added later without the style or the button fails here.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class BannerTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private const string Marker = "{clr-namespace:Theseus.App.Controls}Banner.IsBanner";
    private const string SelectableStyle = "{StaticResource SelectableText}";
    // The dialogs take it as their other styles, dynamically, so a test that builds one without the app's resources still can.
    private const string SelectableStyleInADialog = "{DynamicResource SelectableText}";
    private const string CopyStyle = "{StaticResource BannerCopyButton}";

    /// <summary>Every banner, information bar and notice strip in the app, as REPORT.md lists them.</summary>
    private static readonly (string File, string Name)[] KnownBanners =
    [
        ("MainWindow.xaml", "UpdateBar"),
        ("MainWindow.xaml", "UnconfirmedStopsBar"),
        ("MainWindow.xaml", "HeldForInputsBar"),
        ("MainWindow.xaml", "FilteredLine"),
        ("MainWindow.xaml", "NoticeFlyout"),
        ("Views/ProjectView.xaml", "ChangeBanner"),
        ("Views/ProjectView.xaml", "ClearUpNoticeLine"),
    ];

    /// <summary>
    /// Named like a banner but not one: a card's running sweep and a selected row's mark hold no text; a box's title bar is a
    /// heading; Waits for you is a list of lines, each with its own button, not a message.
    /// </summary>
    private static readonly string[] NotBanners = ["Banner", "SelectionBar", "TitleBar", "WaitingBand"];

    /// <summary>The words a notice's property is named with; a TextBlock bound to one is a notice that cannot be copied.</summary>
    private static readonly string[] NoticeWords = ["Notice", "Banner", "Warning"];

    [Test]
    public void TextOf_IsEveryShownLine_InReadingOrder_OnePerLine()
    {
        Border banner = Banner(out _, "Round 2 began: the plan changed.", "Objectives added or reopened: 1.3, 1.4.");

        Assert.That(Theseus.App.Controls.Banner.TextOf(banner),
            Is.EqualTo("Round 2 began: the plan changed." + Environment.NewLine + "Objectives added or reopened: 1.3, 1.4."));
    }

    [Test]
    public void TextOf_LeavesOutALineThatIsHiddenOrEmpty()
    {
        Border banner = Banner(out List<TextBox> lines, "Shown. ", "", "Hidden.");
        lines[2].Visibility = Visibility.Collapsed;

        Assert.That(Theseus.App.Controls.Banner.TextOf(banner), Is.EqualTo("Shown."), "an offer with no status yet copies without its trailing space");
    }

    [Test]
    public void PressingCopy_PutsTheBannersWholeText_OnTheClipboard()
    {
        Border banner = Banner(out _, "Theseus 1.4.2 is available.", "Downloading, 40%.");
        Button copy = (Button)((DockPanel)banner.Child).Children[0];
        Window window = SelectableTextTests.Host(banner, SelectableTextTests.Shared(), null);
        string? before = Clipboard.ContainsText() ? Clipboard.GetText() : null;

        try
        {
            copy.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            Assert.That(Clipboard.GetText(), Is.EqualTo("Theseus 1.4.2 is available." + Environment.NewLine + "Downloading, 40%."));
        }
        finally
        {
            window.Close();

            if (before is not null)
            {
                Clipboard.SetText(before);
            }
        }
    }

    [Test]
    public void ACopyButton_TakesTheNearestBanner_NotAnOuterOne()
    {
        Border inner = Banner(out _, "Inner.");
        Border outer = new() { Child = inner };
        Theseus.App.Controls.Banner.SetIsBanner(outer, true);
        Button copy = (Button)((DockPanel)inner.Child).Children[0];

        Assert.That(Theseus.App.Controls.Banner.Around(copy), Is.SameAs(inner));
    }

    [Test]
    public void EveryKnownBanner_IsMarked()
    {
        foreach ((string file, string name) in KnownBanners)
        {
            XElement element = Named(file, name);

            Assert.That((string?)element.Attribute(Marker), Is.EqualTo("True"), $"{file} {name} carries controls:Banner.IsBanner");
        }
    }

    [Test]
    public void EveryMarkedBanner_DrawsAllItsTextSelectable_AndHasOneCopyButton()
    {
        List<(string File, XElement Banner)> banners = [.. AppXaml().SelectMany(file => file.Document.Descendants()
            .Where(element => (string?)element.Attribute(Marker) == "True").Select(element => (file.File, element)))];

        Assert.That(banners.Select(banner => (banner.File, (string?)banner.Banner.Attribute(Xaml + "Name"))), Is.EquivalentTo(KnownBanners),
            "a new banner is added to the list above, and to REPORT.md's");

        foreach ((string file, XElement banner) in banners)
        {
            string name = (string?)banner.Attribute(Xaml + "Name") ?? "?";
            List<XElement> outsideButtons = [.. banner.Descendants().Where(element => !InsideAButton(element, banner))];

            Assert.Multiple(() =>
            {
                Assert.That(banner.Descendants(Presentation + "Button").Count(button => (string?)button.Attribute("Style") == CopyStyle), Is.EqualTo(1),
                    $"{file} {name}: one Copy button");
                Assert.That(outsideButtons.Where(element => element.Name == Presentation + "TextBlock" && !IsLabel(element)), Is.Empty,
                    $"{file} {name}: its text is in SelectableText boxes, not TextBlocks");
                Assert.That(outsideButtons.Where(element => element.Name == Presentation + "TextBox")
                        .Select(box => (string?)box.Attribute("Style")), Has.All.EqualTo(SelectableStyle).And.Not.Empty,
                    $"{file} {name}: every text box in it is SelectableText");
            });
        }
    }

    [Test]
    public void EveryCopyButton_SitsInAMarkedBanner()
    {
        foreach ((string file, XDocument document) in AppXaml())
        {
            foreach (XElement button in document.Descendants(Presentation + "Button").Where(button => (string?)button.Attribute("Style") == CopyStyle))
            {
                Assert.That(button.Ancestors().Any(ancestor => (string?)ancestor.Attribute(Marker) == "True"), Is.True,
                    $"{file}: a Copy button with no banner around it copies nothing");
            }
        }
    }

    [Test]
    public void AnythingNamedLikeABanner_ThatHoldsText_IsAMarkedBanner()
    {
        foreach ((string file, XDocument document) in AppXaml())
        {
            IEnumerable<XElement> named = document.Descendants()
                .Where(element => element.Name == Presentation + "Border" || element.Name == Presentation + "DockPanel")
                .Where(element => (string?)element.Attribute(Xaml + "Name") is { } name
                                  && new[] { "Banner", "Bar", "Band", "NoticeLine", "Notice", "Strip" }.Any(end => name.EndsWith(end, StringComparison.Ordinal))
                                  && !NotBanners.Contains(name));

            foreach (XElement element in named)
            {
                Assert.That((string?)element.Attribute(Marker), Is.EqualTo("True"), $"{file} {(string?)element.Attribute(Xaml + "Name")} is a banner");
            }
        }
    }

    [Test]
    public void TheNamedNonBanners_HoldNoMessage_OrAreHeadingsOrLists()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Named("MainWindow.xaml", "Banner").Descendants(Presentation + "TextBlock"), Is.Empty, "the card's running sweep has no text");
            Assert.That(Named("MainWindow.xaml", "SelectionBar").Descendants(Presentation + "TextBlock"), Is.Empty);
            Assert.That(Named("Views/ProjectView.xaml", "WaitingBand").Descendants(Presentation + "ItemsControl"), Is.Not.Empty,
                "Waits for you is a list of lines with buttons");
        });
    }

    [Test]
    public void ABorderThatLooksLikeABanner_IsMarked_NamedOrNot()
    {
        // The board's own strips: docked at the top, padded as a bar, holding wrapped text and buttons. Unnamed, the
        // stop and held-for-inputs bars were found by neither the name nor the binding rule.
        foreach ((string file, XDocument document) in AppXaml())
        {
            IEnumerable<XElement> strips = document.Descendants(Presentation + "Border")
                .Where(border => (string?)border.Attribute("Padding") == "12,8"
                                 && (string?)border.Attribute("Visibility") is { } shown && shown.StartsWith("{Binding", StringComparison.Ordinal)
                                 && border.Descendants().Any(text => text.Name == Presentation + "TextBlock" || text.Name == Presentation + "TextBox")
                                 && !NotBanners.Contains((string?)border.Attribute(Xaml + "Name")));

            foreach (XElement strip in strips)
            {
                Assert.That((string?)strip.Attribute(Marker), Is.EqualTo("True"),
                    $"{file}: the strip {(string?)strip.Attribute(Xaml + "Name") ?? "(unnamed)"} shown by {(string?)strip.Attribute("Visibility")} is a banner");
            }
        }
    }

    [Test]
    public void EveryNoticeBannerOrWarningText_IsSelectable_UnlessItLabelsAButton()
    {
        foreach ((string file, XDocument document) in AppXaml())
        {
            IEnumerable<XElement> texts = document.Descendants(Presentation + "TextBlock")
                .Where(block => (string?)block.Attribute("Text") is { } text && text.StartsWith("{Binding", StringComparison.Ordinal)
                                && BoundProperty(text) is { } property
                                && NoticeWords.Any(word => property.Contains(word, StringComparison.Ordinal)))
                .Where(block => !block.Ancestors().Any(ancestor => ancestor.Name == Presentation + "Button" || ancestor.Name == Presentation + "ToggleButton"));

            Assert.That(texts.Select(block => (string?)block.Attribute("Text")), Is.Empty,
                $"{file}: a notice's text is a SelectableText box, so it can be copied");
        }
    }

    [Test]
    public void EverySelectableTextBinding_IsOneWay_SinceTheBoxNeverWritesBack()
    {
        foreach ((string file, XDocument document) in AppXaml())
        {
            foreach (XElement box in document.Descendants(Presentation + "TextBox").Where(IsSelectable))
            {
                string? text = (string?)box.Attribute("Text");
                string? multi = (string?)box.Element(Presentation + "TextBox.Text")?.Element(Presentation + "MultiBinding")?.Attribute("Mode");

                Assert.That(text?.Contains("Mode=OneWay", StringComparison.Ordinal) == true || multi == "OneWay", Is.True,
                    $"{file}: {text ?? "a multi-binding"} is OneWay");
            }
        }
    }

    private static Border Banner(out List<TextBox> lines, params string[] texts)
    {
        Style style = SelectableTextTests.SelectableText();
        lines = [.. texts.Select(text => new TextBox { Style = style, Text = text })];
        StackPanel stack = new();
        foreach (TextBox line in lines)
        {
            stack.Children.Add(line);
        }

        Button copy = new();
        Theseus.App.Controls.Banner.SetCopies(copy, true);
        DockPanel dock = new();
        DockPanel.SetDock(copy, Dock.Right);
        dock.Children.Add(copy);
        dock.Children.Add(stack);
        Border banner = new() { Child = dock };
        Theseus.App.Controls.Banner.SetIsBanner(banner, true);

        return banner;
    }

    private static bool IsSelectable(XElement box) =>
        (string?)box.Attribute("Style") is SelectableStyle or SelectableStyleInADialog;

    private static bool InsideAButton(XElement element, XElement banner) =>
        element.AncestorsAndSelf().TakeWhile(ancestor => ancestor != banner)
            .Any(ancestor => ancestor.Name == Presentation + "Button" || ancestor.Name == Presentation + "ToggleButton");

    // A fixed heading such as the flyout's "Recent notices" says what the banner is, not what it says.
    private static bool IsLabel(XElement block) =>
        (string?)block.Attribute("Text") is { } text && !text.StartsWith('{');

    private static string? BoundProperty(string binding)
    {
        string inner = binding.Trim('{', '}')["Binding".Length..].Trim();
        string path = inner.Split(',')[0].Trim();

        return path.StartsWith("Path=", StringComparison.Ordinal) ? path["Path=".Length..] : path;
    }

    private static XElement Named(string file, string name) =>
        XDocument.Load(LooseXaml.SourceFile(file.Split('/'))).Descendants().Single(element => (string?)element.Attribute(Xaml + "Name") == name);

    private static IEnumerable<(string File, XDocument Document)> AppXaml()
    {
        string root = LooseXaml.SourceFile();

        return Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => (Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'), XDocument.Load(path)));
    }
}
