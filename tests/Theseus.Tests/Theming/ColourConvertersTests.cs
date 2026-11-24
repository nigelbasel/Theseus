using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Theseus.App.Converters;
using Theseus.App.Services;

namespace Theseus.Tests.Theming;

/// <summary>
/// A user's workstream colour splits by use under a contrast theme (W47, G162): a wash goes, a stripe or border
/// becomes the window-text colour, and a swatch keeps its real colour, since there the colour is the content.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class ColourConvertersTests
{
    private const string Teal = "#0F7B74";

    private Func<bool> _original = null!;

    [SetUp]
    public void RememberTheSeam() => _original = ThemeApplier.IsHighContrast;

    [TearDown]
    public void RestoreTheSeam() => ThemeApplier.IsHighContrast = _original;

    [TestCase(false, "#FF0F7B74")]
    [TestCase(true, "#00FFFFFF")]
    public void AWash_IsTheColour_OrNothingUnderAContrastTheme(bool highContrast, string expected)
    {
        ThemeApplier.IsHighContrast = () => highContrast;

        Assert.That(Colour(new HexToWashConverter(), Teal), Is.EqualTo(expected));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ASwatch_KeepsItsColour_ContrastThemeOrNot(bool highContrast)
    {
        ThemeApplier.IsHighContrast = () => highContrast;

        Assert.That(Colour(new HexToSwatchConverter(), Teal), Is.EqualTo("#FF0F7B74"));
    }

    [Test]
    public void AStripe_IsTheWindowTextColour_UnderAContrastTheme()
    {
        ThemeApplier.IsHighContrast = () => true;

        Assert.That(new HexToBrushConverter().Convert(Teal, typeof(Brush), null!, CultureInfo.InvariantCulture),
            Is.SameAs(SystemColors.WindowTextBrush));
    }

    [Test]
    public void AStripe_IsTheColour_OutsideAContrastTheme()
    {
        ThemeApplier.IsHighContrast = () => false;

        Assert.That(Colour(new HexToBrushConverter(), Teal), Is.EqualTo("#FF0F7B74"));
    }

    [Test]
    public void ACardWithNoColour_DrawsNoStripe_EvenUnderAContrastTheme()
    {
        ThemeApplier.IsHighContrast = () => true;

        Assert.That(Colour(new HexToBrushConverter(), ""), Is.EqualTo("#00FFFFFF"));
    }

    private static string Colour(IValueConverter converter, string hex) =>
        ((SolidColorBrush)converter.Convert(hex, typeof(Brush), null!, CultureInfo.InvariantCulture)).Color.ToString(CultureInfo.InvariantCulture);
}
