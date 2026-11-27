using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml.Linq;
using Theseus.Tests.Theming;

namespace Theseus.Tests.Windows;

/// <summary>
/// The new-release bar stands out (Nigel, 2026-11-24, "yes, it should stand out"): an informational InfoBar on the
/// system accent, with the matching on-accent text, and Highlight and HighlightText under a contrast theme; never the
/// caution amber; the critical red only once an update has stopped (W46; checklist G149, G150, G160).
/// </summary>
[TestFixture]
[NonParallelizable]
public class UpdateBarAccentTests
{
    [Test]
    public void TheOffer_IsOnTheAccent_WithTheOnAccentText() =>
        MainWindowFixture.Run((window, _) =>
        {
            Border bar = (Border)window.FindName("UpdateBar");

            Assert.That(bar.Background, Is.SameAs(window.FindResource("UpdateOfferBrush")));
            Assert.That(TextElement.GetForeground(bar), Is.SameAs(window.FindResource("UpdateOfferTextBrush")));
        });

    [Test]
    public void EachButtonOnTheAccent_HasASolidBacking() =>
        MainWindowFixture.Run((window, _) =>
        {
            List<Button> buttons = [.. MainWindowFixtureTests.Descendants<Button>((Border)window.FindName("UpdateBar"))];

            Assert.That(buttons, Has.Count.EqualTo(4), "Update and restart, Skip this version, Cancel, and Copy (1.3e)");
            // The fixture loads App.xaml, not the Fluent theme, so the solid fill is checked as the reference it is.
            Assert.That(buttons.Select(button => DependencyPropertyHelper.GetValueSource((Border)button.Parent, Border.BackgroundProperty)),
                Has.All.Matches<ValueSource>(source => source.BaseValueSource == BaseValueSource.Local && source.IsExpression),
                "a translucent fill fades on the accent");
        });

    [Test]
    public void ASelectionInTheOffer_IsInTheBarsTextColour_SoItShowsOnHighlightUnderAContrastTheme() =>
        MainWindowFixture.Run((window, _) =>
        {
            TextBox text = (TextBox)window.FindName("UpdateText");

            Assert.That(text.SelectionBrush, Is.SameAs(window.FindResource("UpdateOfferTextBrush")));
        });

    [TestCase("StateColours.Light.xaml", "AccentColorDark1", "#ffffff")]
    [TestCase("StateColours.Dark.xaml", "AccentColorLight2", "#000000")]
    public void EachTheme_TakesTheSystemAccent_AndItsOnAccentText(string file, string accent, string text)
    {
        Dictionary<string, XElement> entries = ThemeFiles.Entries(file);

        Assert.That((string?)entries["UpdateOfferBrush"].Attribute("Color"), Is.EqualTo($"{{x:Static SystemColors.{accent}}}"));
        Assert.That((string?)entries["UpdateOfferTextBrush"].Attribute("Color"), Is.EqualTo(text));
    }

    [Test]
    public void UnderAContrastTheme_TheOfferIsHighlightAndHighlightText()
    {
        Dictionary<string, XElement> entries = ThemeFiles.Entries("StateColours.HighContrast.xaml");

        Assert.That((string?)entries["UpdateOfferBrush"].Attribute("Color"), Is.EqualTo("{x:Static SystemColors.HighlightColor}"));
        Assert.That((string?)entries["UpdateOfferTextBrush"].Attribute("Color"), Is.EqualTo("{x:Static SystemColors.HighlightTextColor}"));
    }
}
