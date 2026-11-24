using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// A card's Setup window as written in its XAML: Enter saves and Esc cancels through the dialog's own buttons, the commit
/// comes first, every field is named by its label, and each access key is used once (baseline WB4: B13, A4, E12, A9,
/// A23, A-m3, A13, B9; decisions W9, W12, W14, W20, W26, W43, W50).
/// </summary>
[TestFixture]
public class SetupMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Controls = "clr-namespace:Theseus.App.Controls";

    [Test]
    public void Save_IsTheDefault_AndTheAccentedCommit()
    {
        XElement save = Button("_Save");

        Assert.That(((string?)save.Attribute("IsDefault"), (string?)save.Attribute("Style")), Is.EqualTo(("True", "{DynamicResource AccentButton}")));
    }

    [Test]
    public void Cancel_IsTheCancelButton()
    {
        XElement cancel = Button("Cancel");

        Assert.That(((string?)cancel.Attribute("IsCancel"), (string?)cancel.Attribute("Style")), Is.EqualTo(("True", "{DynamicResource DialogButton}")));
    }

    [Test]
    public void Save_ComesBeforeCancel()
    {
        List<string?> buttons = [.. Setup().Descendants(Presentation + "Button").Select(button => (string?)button.Attribute("Content"))];

        Assert.That(buttons.IndexOf("_Save"), Is.LessThan(buttons.IndexOf("Cancel")).And.GreaterThanOrEqualTo(0), "the commit on the left (W9, G12)");
    }

    [Test]
    public void Escape_IsLeftToTheCancelButton()
    {
        Assert.That(Setup().Descendants(Presentation + "KeyBinding").Where(binding => (string?)binding.Attribute("Key") == "Escape"), Is.Empty);
    }

    [Test]
    public void TheNewDirectoryBox_AddsOnEnter()
    {
        XElement box = Setup().Descendants(Presentation + "TextBox")
            .Single(element => ((string?)element.Attribute("Text"))?.Contains("Drawer.NewDirectory") == true);

        Assert.That(box.Descendants(Presentation + "KeyBinding").Select(binding => ((string?)binding.Attribute("Key"), (string?)binding.Attribute("Command"))),
            Is.EqualTo(new[] { ((string?)"Return", (string?)"{Binding Drawer.AddDirectoryCommand}") }));
    }

    [TestCase("FirstRunBox", "FirstRunLabel")]
    [TestCase("EveryStartBox", "EveryStartLabel")]
    public void EachPromptBox_IsNamedByItsLabel(string box, string label)
    {
        XElement markdown = Setup().Descendants(Controls + "MarkdownBox").Single(element => (string?)element.Attribute(Xaml + "Name") == box);

        Assert.That((string?)markdown.Attribute("AutomationProperties.LabeledBy"), Is.EqualTo($"{{Binding ElementName={label}}}"));
        Assert.That(Setup().Descendants().Any(element => (string?)element.Attribute(Xaml + "Name") == label), Is.True);
    }

    [Test]
    public void EachTextField_HasALabelThatTargetsIt()
    {
        HashSet<string> targets = [.. Setup().Descendants(Presentation + "Label").Select(label => (string?)label.Attribute("Target") ?? string.Empty)];

        foreach (XElement box in Setup().Descendants(Presentation + "TextBox"))
        {
            string name = (string?)box.Attribute(Xaml + "Name") ?? "(unnamed)";

            Assert.That(targets, Does.Contain($"{{Binding ElementName={name}}}"), $"{name}'s label carries its access key (W50)");

            string? labelledBy = (string?)box.Attribute("AutomationProperties.LabeledBy");
            XElement? label = Setup().Descendants(Presentation + "Label").SingleOrDefault(element => (string?)element.Attribute("Target") == $"{{Binding ElementName={name}}}");

            Assert.That(labelledBy, Is.EqualTo($"{{Binding ElementName={(string?)label?.Attribute(Xaml + "Name")}}}"),
                $"{name} is named by its visible label; Label.Target alone leaves the box unnamed to a screen reader (W50, G168)");
        }
    }

    [Test]
    public void ItOpens_WithTheFirstFieldFocused()
    {
        Assert.That((string?)Setup().Root!.Attribute("FocusManager.FocusedElement"), Is.EqualTo("{Binding ElementName=WorkingDirectoryBox}"), "W43, G66");
    }

    [Test]
    public void OpeningAFolderOrFile_IsALink()
    {
        Assert.That(Setup().Descendants(Presentation + "Button")
                .Where(button => ((string?)button.Attribute("Command"))?.Contains("Drawer.Open") == true)
                .Select(button => (string?)button.Attribute("Style")),
            Is.EqualTo(new[] { "{DynamicResource LinkButton}", "{DynamicResource LinkButton}" }), "opening something outside Theseus is navigation (W12, W15)");
    }

    [Test]
    public void AccessKeys_AreEachUsedOnce()
    {
        List<char> keys = [.. AccessKeys(Setup().Root!)];

        Assert.That(keys, Is.Unique);
        Assert.That(keys, Has.Count.GreaterThanOrEqualTo(6));
        Assert.That(keys, Has.None.EqualTo('I').And.None.EqualTo('O'), "I and O read as 1 and 0 (W26, G82)");
    }

    [TestCase("Instructions and skills…")]
    [TestCase("This directory is missing.")]
    [TestCase("Order in the lane\"")]
    public void OldWording_IsGone(string words)
    {
        Assert.That(File.ReadAllText(Path.Combine(AppFolder(), "SetupWindow.xaml")), Does.Not.Contain(words));
    }

    [TestCase("This folder does not exist. Choose another, or create it first.")]
    [TestCase("Order in the _lane:")]
    public void NewWording_IsThere(string words)
    {
        Assert.That(File.ReadAllText(Path.Combine(AppFolder(), "SetupWindow.xaml")), Does.Contain(words));
    }

    private static IEnumerable<char> AccessKeys(XElement root) =>
        root.DescendantsAndSelf()
            .Where(element => !element.Ancestors().Any(ancestor => ancestor.Name.LocalName == "DataTemplate"))
            .SelectMany(element => new[] { element.Attribute("Content"), element.Attribute("Header") })
            .Where(attribute => attribute is not null && !attribute.Value.StartsWith('{'))
            .Select(attribute => attribute!.Value)
            .Select(text => text.IndexOf('_') is int at and >= 0 && at + 1 < text.Length ? char.ToUpperInvariant(text[at + 1]) : '\0')
            .Where(key => key != '\0');

    private static XElement Button(string content) =>
        Setup().Descendants(Presentation + "Button").Single(button => (string?)button.Attribute("Content") == content);

    private static XDocument Setup() => XDocument.Load(Path.Combine(AppFolder(), "SetupWindow.xaml"));

    private static string AppFolder([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "..", "src", "Theseus.App"));
}
