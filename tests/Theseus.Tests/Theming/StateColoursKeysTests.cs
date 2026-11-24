using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace Theseus.Tests.Theming;

/// <summary>
/// The three state-colour dictionaries define the same keys, and under a contrast theme every brush is one of the
/// user's system colours (W47; G162, G165), so nothing Theseus draws keeps its own hue there.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class StateColoursKeysTests
{
    private const string SystemColour = @"^\{x:Static SystemColors\.[A-Za-z]+Color\}$";

    [Test]
    public void TheLightAndDarkDictionaries_DefineTheSameKeys()
    {
        Assert.That(ThemeFiles.Entries("StateColours.Dark.xaml").Keys, Is.EquivalentTo(ThemeFiles.Entries("StateColours.Light.xaml").Keys));
    }

    [Test]
    public void TheHighContrastDictionary_DefinesEveryKeyTheLightOneDoes()
    {
        Assert.That(ThemeFiles.Entries("StateColours.HighContrast.xaml").Keys,
            Is.EquivalentTo(ThemeFiles.Entries("StateColours.Light.xaml").Keys));
    }

    [Test]
    public void EveryHighContrastBrush_IsASystemColour()
    {
        var brushes = ThemeFiles.Entries("StateColours.HighContrast.xaml")
            .Where(entry => entry.Value.Name == ThemeFiles.Presentation + "SolidColorBrush")
            .ToList();

        Assert.That(brushes, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            foreach (var (key, brush) in brushes)
            {
                Assert.That((string?)brush.Attribute("Color"), Does.Match(SystemColour), key);
            }
        });
    }

    [TestCase("SearchHighlightBrush", "HighlightColor")]
    [TestCase("SearchHighlightTextBrush", "HighlightTextColor")]
    [TestCase("ConnectorBrush", "WindowTextColor")]
    [TestCase("LinkBrush", "HotTrackColor")]
    public void AKeyUsedFromCode_MapsToItsSystemColour(string key, string systemColour)
    {
        Assert.That((string?)ThemeFiles.Entries("StateColours.HighContrast.xaml")[key].Attribute("Color"),
            Is.EqualTo($"{{x:Static SystemColors.{systemColour}}}"));
    }

    [Test]
    public void TheHighContrastDictionary_LoadsTheSystemsOwnColours_AndAThickerFlyoutEdge()
    {
        var dictionary = (ResourceDictionary)XamlReader.Parse(ThemeFiles.Text("StateColours.HighContrast.xaml"));

        Assert.That(((SolidColorBrush)dictionary["SearchHighlightBrush"]).Color, Is.EqualTo(SystemColors.HighlightColor));
        Assert.That(((SolidColorBrush)dictionary["StateDoneBrush"]).Color, Is.EqualTo(SystemColors.WindowTextColor));
        Assert.That(dictionary["FlyoutBorderThickness"], Is.EqualTo(new Thickness(2)));
    }
}
