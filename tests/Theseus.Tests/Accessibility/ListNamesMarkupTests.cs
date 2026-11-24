using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Accessibility;

/// <summary>
/// Every list in the app's XAML says what it is, and each of its items what it shows (baseline W50, E11, E16; checklist
/// G167, G170). The tree walk of <see cref="AutomationTreeTests"/> sees only the lists its fakes fill; this reads every
/// one, so a list that is empty in a test, such as Locks or Ready, cannot keep reading its items' record dumps.
/// </summary>
[TestFixture]
public class ListNamesMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly HashSet<string> Lists = new(StringComparer.Ordinal) { "ItemsControl", "ListBox", "ListView", "TreeView" };

    // Lists whose items are strings, by file and binding: a string item is read as itself, so it needs no container style.
    private static readonly HashSet<(string File, string Source)> StringItems =
    [
        ("MainWindow.xaml", "{Binding Tags}"), ("MainWindow.xaml", "{Binding Suggestions}"), ("MainWindow.xaml", "{Binding Drawer.JiraKeys}"),
        ("MainWindow.xaml", "{Binding Drawer.Events}"), ("ProjectPropertiesWindow.xaml", "{Binding Tags}"),
        ("ProjectPropertiesWindow.xaml", "{Binding Suggestions}"), ("SetupWindow.xaml", "{Binding Drawer.AddDirectories}"),
        ("SettingsWindow.xaml", "{Binding HiddenProjectFolders}"), ("Views/ProjectView.xaml", "{Binding CrossMarks}"),
        ("Views/ProjectView.xaml", "{Binding Tallies}"), ("Views/ProjectView.xaml", "{Binding Links}"),
    ];

    // Where a list's naming style may come from: its own file, or the app's shared dictionary.
    private const string Shared = "Themes/Interactive.xaml";

    [Test]
    public void EveryList_HasAName()
    {
        List<string> unnamed = [.. EveryList().Where(list => !IsNamed(list.Element)).Select(list => list.Where)];

        Assert.That(unnamed, Is.Empty, Environment.NewLine + string.Join(Environment.NewLine, unnamed));
    }

    [Test]
    public void EveryListsItems_AreNamedByWhatTheyShow()
    {
        List<string> unnamed = [.. EveryList().Where(list => !StringItems.Contains((list.File, (string?)list.Element.Attribute("ItemsSource") ?? string.Empty))
                && !NamesItsItems(list.Element, NamingStyles(list.File))).Select(list => list.Where)];

        Assert.That(unnamed, Is.Empty, Environment.NewLine + string.Join(Environment.NewLine, unnamed));
    }

    private static bool IsNamed(XElement list) =>
        list.Attributes().Any(attribute => attribute.Name.LocalName is "AutomationProperties.Name" or "AutomationProperties.LabeledBy");

    private static bool NamesItsItems(XElement list, HashSet<string> namingStyles)
    {
        if (Referenced((string?)list.Attribute("ItemContainerStyle")) is { } key)
        {
            return namingStyles.Contains(key);
        }

        return list.Elements().Where(child => child.Name.LocalName.EndsWith(".ItemContainerStyle", StringComparison.Ordinal))
            .SelectMany(child => child.Elements(Presentation + "Style")).Any(style => SetsName(style) || BasedOnNaming(style, namingStyles));
    }

    private static bool BasedOnNaming(XElement style, HashSet<string> namingStyles) =>
        Referenced((string?)style.Attribute("BasedOn")) is { } basis && namingStyles.Contains(basis);

    // A setter that names the item from the item: a constant name would call every row the same.
    private static bool SetsName(XElement style) =>
        style.Elements(Presentation + "Setter").Any(setter => (string?)setter.Attribute("Property") == "AutomationProperties.Name"
            && ((string?)setter.Attribute("Value") ?? string.Empty).StartsWith("{Binding", StringComparison.Ordinal));

    private static HashSet<string> NamingStyles(string file) =>
        new(Documents().Where(document => document.File == file || document.File == Shared).SelectMany(document => document.Document.Descendants(Presentation + "Style"))
            .Where(SetsName).Select(style => (string?)style.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Key"))
            .Where(key => key is not null).Select(key => key!), StringComparer.Ordinal);

    private static IEnumerable<(XElement Element, string File, string Where)> EveryList() =>
        Documents().SelectMany(document => document.Document.Descendants().Where(element => element.Name.Namespace == Presentation && Lists.Contains(element.Name.LocalName))
            .Select(element => (element, document.File, $"{document.File}:{((System.Xml.IXmlLineInfo)element).LineNumber} {element.Name.LocalName} " +
                $"{(string?)element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Name") ?? (string?)element.Attribute("ItemsSource")}")));

    private static IEnumerable<(string File, XDocument Document)> Documents() =>
        Directory.EnumerateFiles(AppRoot(), "*.xaml", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(AppRoot(), path).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(path => !path.StartsWith("bin/", StringComparison.Ordinal) && !path.StartsWith("obj/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(path => (path, XDocument.Load(Path.Combine(AppRoot(), path), LoadOptions.SetLineInfo)));

    private static string? Referenced(string? markup)
    {
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(markup ?? string.Empty,
            @"^\{(?:StaticResource|DynamicResource)\s+(?:ResourceKey=)?([\w.]+)\}$");

        return match.Success ? match.Groups[1].Value : null;
    }

    private static string AppRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "..", "src", "Theseus.App"));
}
