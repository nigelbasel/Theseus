using System.Windows;
using System.Windows.Media;
using Theseus.App.Services;

namespace Theseus.Tests.Theming;

/// <summary>
/// Under a contrast theme Theseus merges its high-contrast state colours whatever theme Options names, and swaps back
/// when the contrast theme is turned off (W47; G162, G165).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
[NonParallelizable]
public class ThemeApplierHighContrastTests
{
    private Func<bool> _original = null!;

    [SetUp]
    public void RememberTheSeam() => _original = ThemeApplier.IsHighContrast;

    [TearDown]
    public void RestoreTheSeam() => ThemeApplier.IsHighContrast = _original;

    [TestCase(false)]
    [TestCase(true)]
    public void UnderAContrastTheme_TheHighContrastColoursAreMerged_WhateverTheTheme(bool dark)
    {
        var resources = new ResourceDictionary();
        ThemeApplier.IsHighContrast = () => true;

        ThemeApplier.ApplyStateColours(resources, dark);

        Assert.That(Sources(resources), Is.EqualTo(new[] { "StateColours.HighContrast.xaml" }));
        Assert.That(((SolidColorBrush)resources["StateDoneBrush"]).Color, Is.EqualTo(SystemColors.WindowTextColor));
    }

    [Test]
    public void WhenTheContrastThemeIsTurnedOff_TheThemesOwnColoursReplaceTheContrastOnes()
    {
        var resources = new ResourceDictionary();
        ThemeApplier.IsHighContrast = () => true;
        ThemeApplier.ApplyStateColours(resources, dark: true);
        ThemeApplier.IsHighContrast = () => false;

        ThemeApplier.ApplyStateColours(resources, dark: true);

        Assert.That(Sources(resources), Is.EqualTo(new[] { "StateColours.Dark.xaml" }));
    }

    [Test]
    public void UnderAContrastTheme_ApplyingAgain_LoadsTheDictionaryAfresh_SoAChangedContrastThemeIsRead()
    {
        var resources = new ResourceDictionary();
        ThemeApplier.IsHighContrast = () => true;
        ThemeApplier.ApplyStateColours(resources, dark: false);
        var first = resources.MergedDictionaries.Single();

        ThemeApplier.ApplyStateColours(resources, dark: false);

        Assert.That(resources.MergedDictionaries.Single(), Is.Not.SameAs(first));
    }

    [Test]
    public void OutsideAContrastTheme_ApplyingTheSameThemeAgain_KeepsTheMergedDictionary()
    {
        var resources = new ResourceDictionary();
        ThemeApplier.IsHighContrast = () => false;
        ThemeApplier.ApplyStateColours(resources, dark: true);
        var first = resources.MergedDictionaries.Single();

        ThemeApplier.ApplyStateColours(resources, dark: true);

        Assert.That(resources.MergedDictionaries.Single(), Is.SameAs(first));
    }

    [Test]
    public void OutsideAContrastTheme_TheLightColoursAreMergedForTheLightTheme()
    {
        var resources = new ResourceDictionary();
        ThemeApplier.IsHighContrast = () => false;

        ThemeApplier.ApplyStateColours(resources, dark: false);

        Assert.That(Sources(resources), Is.EqualTo(new[] { "StateColours.Light.xaml" }));
    }

    private static IEnumerable<string> Sources(ResourceDictionary resources) =>
        resources.MergedDictionaries.Select(dictionary => Path.GetFileName(dictionary.Source.OriginalString));
}
