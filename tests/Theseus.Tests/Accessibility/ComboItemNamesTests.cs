using System.Xml.Linq;
using Theseus.Tests.Conventions;

namespace Theseus.Tests.Accessibility;

/// <summary>
/// A combo box's items are named through its ItemContainerStyle (W50, G170): DisplayMemberPath changes what is drawn, not
/// what a screen reader hears, which is otherwise a record's ToString, such as "ProjectOption { Name = … }".
/// </summary>
[TestFixture]
public class ComboItemNamesTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [TestCase("AddToProjectWindow.xaml", "ProjectBox", "{Binding Title}")]
    [TestCase("SettingsWindow.xaml", "ThemeBox", "{Binding Label}")]
    [TestCase("MainWindow.xaml", "LinkKind", "{Binding Label}")]
    [TestCase("MainWindow.xaml", "LinkTarget", "{Binding Title}")]
    public void AComboOfRecords_NamesEachItemByWhatItShows(string file, string box, string name) =>
        Assert.That(XDocument.Load(LooseXaml.SourceFile(file)).Descendants(Presentation + "ComboBox")
            .Single(each => (string?)each.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Name") == box)
            .Descendants(Presentation + "Setter").Single(setter => (string?)setter.Attribute("Property") == "AutomationProperties.Name")
            .Attribute("Value")!.Value, Is.EqualTo(name));
}
