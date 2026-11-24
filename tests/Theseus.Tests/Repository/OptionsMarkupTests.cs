using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// Options as written in its XAML: a settings page with no Save, one Close, seven tabs, and a toggle switch for a setting
/// that takes effect at once (W6, W7, W40; checklist G138, G122, G127).
/// </summary>
[TestFixture]
public class OptionsMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void NoButton_SaysSave()
    {
        Assert.That(Buttons().Select(button => (string?)button.Attribute("Content")).Where(content => content is "Save" or "_Save"), Is.Empty);
    }

    [Test]
    public void ItHasSevenTabs()
    {
        Assert.That(Options().Descendants(Presentation + "TabItem").Count(), Is.EqualTo(7), "a recorded deviation (W7)");
    }

    [Test]
    public void ItsOnlyCancelButton_IsClose()
    {
        Assert.That(Buttons().Where(button => (string?)button.Attribute("IsCancel") == "True").Select(button => (string?)button.Attribute("Content")),
            Is.EqualTo(new[] { "Close" }));
    }

    [Test]
    public void PreReleases_IsAToggleSwitch()
    {
        XElement? switchElement = Options().Descendants(Presentation + "ToggleButton")
            .SingleOrDefault(element => ((string?)element.Attribute("IsChecked"))?.Contains("IncludePreReleases") == true);

        Assert.That(switchElement, Is.Not.Null, "a setting that applies at once is a toggle switch (W40, G127)");
        Assert.That((string?)switchElement!.Attribute("Style"), Is.EqualTo("{DynamicResource ToggleSwitch}"));
        Assert.That(Options().Descendants(Presentation + "CheckBox"), Is.Empty);
    }

    [TestCase("once saved")]
    [TestCase("Cancel does not undo")]
    public void NoTextPromisesASave(string words)
    {
        Assert.That(File.ReadAllText(Path.Combine(AppFolder(), "SettingsWindow.xaml")), Does.Not.Contain(words));
    }

    private static IEnumerable<XElement> Buttons() => Options().Descendants(Presentation + "Button");

    private static XDocument Options() => XDocument.Load(Path.Combine(AppFolder(), "SettingsWindow.xaml"));

    private static string AppFolder([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "..", "src", "Theseus.App"));
}
