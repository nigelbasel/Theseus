using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// Options' wording, keys and layout as written in its XAML (baseline WB4 PR 2: A8, A9, A13, B9, A23, A24, B11, B-m1,
/// B-m2, B16, D8, D17, D-m2, D16, C22, B2; decisions W7, W14, W17, W20, W26, W42, W43, W53, W54).
/// </summary>
[TestFixture]
public class OptionsTextAndKeysTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Controls = "clr-namespace:Theseus.App.Controls";

    [TestCase("Start mode")]
    [TestCase("Identity prompt")]
    [TestCase("Continue prompt")]
    [TestCase("Stop prompt")]
    public void SettingHeaders_AreNouns(string label)
    {
        Assert.That(Options().Descendants(Controls + "SettingRow").Select(row => (string?)row.Attribute("Label")), Does.Contain(label), "D17, W17");
    }

    [TestCase("When a workstream has run before")]
    [TestCase("Who the session is\"")]
    [TestCase("Continuing a workstream\"")]
    [TestCase("Stopping a session\"")]
    [TestCase("Built-in")]
    [TestCase("Keep it")]
    [TestCase("Project properties…")]
    [TestCase("Text=\"Colour\"")]
    [TestCase("_Install")]
    [TestCase("Choose _projects")]
    [TestCase("Choose _worktrees")]
    public void OldWording_IsGone(string words)
    {
        Assert.That(Text(), Does.Not.Contain(words));
    }

    [Test]
    public void TheIdentityRow_ResetsToTheBuiltInWording()
    {
        XElement reset = ButtonIn("IdentityRow", "_Reset");

        Assert.That((string?)reset.Attribute("ToolTip"), Is.EqualTo("Use the built-in wording."));
    }

    [Test]
    public void TheHoldoutRemote_IsCleared_NotReset()
    {
        XElement clear = Options().Descendants(Presentation + "Button")
            .Single(button => (string?)button.Attribute("Command") == "{Binding ResetHoldoutRemoteCommand}");

        Assert.That(((string?)clear.Attribute("Content"), (string?)clear.Attribute("ToolTip")), Is.EqualTo(("_Clear", "Remove the address.")));
    }

    [Test]
    public void TheColourLabel_EndsWithAColon()
    {
        Assert.That(Text(), Does.Contain("Text=\"Colour:\""), "a label in front of its control (W20, G100)");
    }

    [Test]
    public void TheStartModeGroup_IsOneTabStop_WithArrowsThatDoNotWrap()
    {
        XElement group = Options().Descendants(Presentation + "RadioButton").First().Parent!;

        Assert.That((string?)group.Attribute("KeyboardNavigation.DirectionalNavigation"), Is.EqualTo("Contained"), "W42, G72");
        Assert.That(group.Elements(Presentation + "RadioButton").Select(radio => (string?)radio.Attribute("IsTabStop")),
            Is.All.EqualTo("{Binding IsChecked, RelativeSource={RelativeSource Self}}"), "one Tab stop, the choice in force (W42, G67)");
        Assert.That(group.Elements(Presentation + "RadioButton").Select(radio => (string?)radio.Attribute(Controls + "SelectorBar.SelectsOnFocus")),
            Is.All.EqualTo("True"), "a radio button is checked when it receives keyboard focus");
    }

    [Test]
    public void ThePageWidth_IsCapped()
    {
        XElement page = Options().Descendants(Presentation + "Style").Single(style => (string?)style.Attribute(Xaml + "Key") == "Page");

        Assert.That(page.Elements(Presentation + "Setter").Select(setter => ((string?)setter.Attribute("Property"), (string?)setter.Attribute("Value"))),
            Does.Contain(((string?)"MaxWidth", (string?)"1000")).And.Contain(((string?)"HorizontalAlignment", (string?)"Left")), "W7, G139");
    }

    [Test]
    public void TheLayoutLink_SaysWhereItGoes()
    {
        XElement link = Options().Descendants(Presentation + "Hyperlink").Single(element => ((string?)element.Attribute("NavigateUri"))?.Contains("project-layout") == true);

        Assert.That((string?)link.Attribute("ToolTip"), Is.EqualTo((string?)link.Attribute("NavigateUri")), "W54, G156");
    }

    [Test]
    public void Advanced_EndsWithAnAboutSection_ThatOpensAbout()
    {
        XElement advanced = Tab("Advanced");
        XElement last = advanced.Descendants(Presentation + "StackPanel").First().Elements().Last();

        Assert.That((string?)last.Attribute(Xaml + "Name"), Is.EqualTo("AboutSection"), "W53, G143");
        Assert.That(last.Descendants().Any(element => ((string?)element.Attribute("Text"))?.Contains("VersionDescribed") == true), Is.True, "name and version");
        Assert.That(last.Descendants(Presentation + "Hyperlink").Select(link => (string?)link.Attribute("Click")), Does.Contain("OnAbout"));
    }

    [Test]
    public void TheTabs_ComeBeforeTheFooter_SoTabReachesTheContentFirst()
    {
        List<XElement> elements = [.. Options().Descendants()];
        XElement tabs = elements.Single(element => element.Name == Presentation + "TabControl");
        XElement close = elements.Single(element => element.Name == Presentation + "Button" && (string?)element.Attribute("Content") == "Close");

        Assert.That(elements.IndexOf(tabs), Is.LessThan(elements.IndexOf(close)), "B2, W43, G65");
    }

    [Test]
    public void TheTagSwatches_KeepTheirRealColour()
    {
        Assert.That(Text(), Does.Not.Contain("Converter={StaticResource HexToBrush}"), "a swatch keeps its colour in a contrast theme (W47)");
        Assert.That(Text(), Does.Contain("Converter={StaticResource HexToSwatch}"));
    }

    [Test]
    public void EachTab_UsesEachAccessKeyOnce_AndNeverIOrO()
    {
        foreach (XElement tab in Options().Descendants(Presentation + "TabItem"))
        {
            List<char> keys = [.. AccessKeys(tab)];

            Assert.That(keys, Is.Unique, (string?)tab.Attribute("Header"));
            Assert.That(keys, Has.None.EqualTo('I').And.None.EqualTo('O'), (string?)tab.Attribute("Header"));
        }
    }

    [TestCase("General", 'C')]
    [TestCase("General", 'P')]
    [TestCase("Folders", 'T')]
    [TestCase("Guards", 'W')]
    [TestCase("Prompts", 'R')]
    [TestCase("Advanced", 'C')]
    public void CommonCommands_HaveAnAccessKey(string tab, char key)
    {
        Assert.That(AccessKeys(Tab(tab)), Does.Contain(key), "W26, G34");
    }

    [Test]
    public void BrowseButtonsAndClose_HaveNoAccessKey()
    {
        IEnumerable<string> contents = Options().Descendants(Presentation + "Button")
            .Where(button => ((string?)button.Attribute("Click"))?.StartsWith("OnChoose") == true || (string?)button.Attribute("Content") is "Close")
            .Select(button => (string?)button.Attribute("Content") ?? string.Empty);

        Assert.That(contents, Has.None.Contains("_"), "W26, B-m1");
    }

    [Test]
    public void EachAccessKey_FollowsTheLetterRule()
    {
        IEnumerable<string> labels = Options().Descendants().Select(element => (string?)element.Attribute("Content") ?? string.Empty)
            .Where(label => label.Contains('_') && !label.StartsWith('{'));

        Assert.That(labels.Where(label => !FollowsTheLetterRule(label)), Is.Empty, "W26, B11");
    }

    [Test]
    public void AValuePrompt_SaysSave_NotOK()
    {
        Assert.That(File.ReadAllText(Path.Combine(AppFolder(), "SettingsWindow.xaml.cs")), Does.Contain("ActionLabel = \"_Save\""), "A-m2, G9");
    }

    // W26 (G82 over G83): the first letter of a word in the label, or else a letter that is not on G83's avoid list.
    private static bool FollowsTheLetterRule(string label)
    {
        int at = label.IndexOf('_');
        bool startsAWord = at == 0 || label[at - 1] == ' ';

        return startsAWord || !"ilgjpqy".Contains(char.ToLowerInvariant(label[at + 1]));
    }

    private static IEnumerable<char> AccessKeys(XElement root) =>
        root.DescendantsAndSelf()
            .Where(element => !element.Ancestors().Any(ancestor => ancestor.Name.LocalName == "DataTemplate"))
            .Where(element => element.Name.LocalName != "TabItem")
            .Select(element => element.Attribute("Content"))
            .Where(attribute => attribute is not null && !attribute.Value.StartsWith('{'))
            .Select(attribute => attribute!.Value)
            .Select(text => text.IndexOf('_') is int at and >= 0 && at + 1 < text.Length ? char.ToUpperInvariant(text[at + 1]) : '\0')
            .Where(key => key != '\0');

    private static XElement ButtonIn(string rowName, string content) =>
        Options().Descendants(Controls + "SettingRow").Single(row => (string?)row.Attribute(Xaml + "Name") == rowName)
            .Descendants(Presentation + "Button").Single(button => (string?)button.Attribute("Content") == content);

    private static XElement Tab(string header) =>
        Options().Descendants(Presentation + "TabItem").Single(tab => (string?)tab.Attribute("Header") == header);

    private static string Text() => File.ReadAllText(Path.Combine(AppFolder(), "SettingsWindow.xaml"));

    private static XDocument Options() => XDocument.Load(Path.Combine(AppFolder(), "SettingsWindow.xaml"));

    private static string AppFolder([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "..", "src", "Theseus.App"));
}
