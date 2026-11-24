using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml.Linq;

namespace Theseus.Tests.Conventions;

/// <summary>
/// The shared styles and tokens of Windows conventions batch WB1 (baseline W11, W12, W16, W18, W19, W22, W24, W36 to W38,
/// W40, W56). Loaded on its own, Interactive.xaml cannot resolve the Fluent theme's keys, so these tests assert the key a
/// setter names, never the brush it resolves to.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class SharedStylesTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly double[] Ramp = [12, 14, 18, 20, 28];

    [Test]
    public void ACommandButton_DrawsItsAccessKey_AndAltWithThatLetterIsRegistered()
    {
        Button button = new() { Content = "_Approve", Style = (Style)Shared()["CommandButton"] };
        Window window = Host(button);

        try
        {
            Assert.That(AccessKeyManager.IsKeyRegistered(PresentationSource.FromVisual(window), "A"), Is.True, "Alt+A reaches the button (B1)");
            Assert.That(Descendants(button).OfType<TextBlock>().Select(text => text.Text), Has.None.EqualTo("_Approve"),
                "the underscore is not drawn");
        }
        finally
        {
            window.Close();
        }
    }

    [Test]
    public void ALink_IsUnderlinedAtRest()
    {
        Button link = new() { Content = "Open folder", Style = (Style)Shared()["LinkButton"] };
        Window window = Host(link);

        try
        {
            Border underline = (Border)link.Template.FindName("Underline", link);

            Assert.That(underline.BorderThickness.Bottom, Is.GreaterThan(0));
            Assert.That(underline.BorderBrush, Is.Not.Null.And.Not.EqualTo(Brushes.Transparent), "W16: a link is underlined at rest");
            Assert.That(((SolidColorBrush)underline.BorderBrush).Color.A, Is.GreaterThan(0));
        }
        finally
        {
            window.Close();
        }
    }

    [Test]
    public void TheBareLinkTemplate_IsUnderlinedOnlyOnHover_SoTheProjectViewsChipsAndRowsAreNotDrawnAsLinks()
    {
        Button chip = new() { Content = "3 waiting on you", Template = (ControlTemplate)Shared()["LinkTemplate"] };
        Window window = Host(chip);

        try
        {
            Border underline = (Border)chip.Template.FindName("Underline", chip);

            Assert.That(underline.BorderBrush, Is.EqualTo(Brushes.Transparent));
        }
        finally
        {
            window.Close();
        }
    }

    [Test]
    public void ALinksHover_ThickensItsUnderline_AndKeepsTheLinksOwnColour()
    {
        ControlTemplate template = (ControlTemplate)StyleSetter((Style)Shared()["LinkButton"], "Template")!;
        Trigger hover = template.Triggers.OfType<Trigger>().Single(trigger => trigger.Property.Name == "IsMouseOver");

        Assert.That(hover.Setters.OfType<Setter>().Select(setter => (setter.TargetName, setter.Property.Name, setter.Value)),
            Is.EqualTo(new[] { ("Underline", "BorderThickness", (object)new Thickness(0, 0, 0, 2)) }), "W16: weight on hover, no colour of its own");
    }

    [TestCase("CaptionText", 12)]
    [TestCase("BodyText", 14)]
    [TestCase("BodyStrongText", 14)]
    [TestCase("BodyLargeText", 18)]
    [TestCase("SubtitleText", 20)]
    [TestCase("TitleText", 28)]
    [TestCase("ListItemTitle", 14)]
    [TestCase("ListItemCaption", 12)]
    [TestCase("ListSectionHeader", 14)]
    [TestCase("TagText", 12)]
    [TestCase("FieldLabel", 12)]
    public void ATypeRampStyle_IsDefinedOnceInAppXaml_AtARampSize(string key, double size)
    {
        XElement style = AppStyle(key);

        Assert.That(SetterValue(style, "FontSize"), Is.EqualTo(size.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.That(Ramp, Does.Contain(size));
    }

    [Test]
    public void EveryTextStyleInAppXaml_IsOnTheRamp_AndSemiboldOnlyAtFourteenOrMore()
    {
        List<XElement> styles = [.. AppXaml().Descendants(Presentation + "Style").Where(style => SetterValue(style, "FontSize") is not null)];

        Assert.That(styles, Is.Not.Empty);

        foreach (XElement style in styles)
        {
            double size = double.Parse(SetterValue(style, "FontSize")!, System.Globalization.CultureInfo.InvariantCulture);
            string key = (string?)style.Attribute(Xaml + "Key") ?? "?";

            Assert.That(Ramp, Does.Contain(size), key);

            if (SetterValue(style, "FontWeight") is "SemiBold" or "Bold")
            {
                Assert.That(size, Is.GreaterThanOrEqualTo(14), $"{key}: G96, no semibold under 14");
            }
        }
    }

    [Test]
    public void TheListSectionHeader_IsBodyStrong_AndTheTagIsCaptionInTheUiFontAndSecondaryBrush()
    {
        Assert.That(SetterValue(AppStyle("ListSectionHeader"), "FontWeight"), Is.EqualTo("SemiBold"));
        Assert.That(SetterValue(AppStyle("TagText"), "FontFamily"), Is.EqualTo("{DynamicResource UiFont}"));
        Assert.That(SetterValue(AppStyle("TagText"), "Foreground"), Is.EqualTo("{DynamicResource TextFillColorSecondaryBrush}"));
        Assert.That(SetterValue(AppStyle("ListItemCaption"), "Foreground"), Is.EqualTo("{DynamicResource TextFillColorSecondaryBrush}"));
    }

    [Test]
    public void AFieldLabel_Sits8AboveItsControl_And12BelowWhatIsAbove()
    {
        Assert.That(SetterValue(AppStyle("FieldLabel"), "Margin"), Is.EqualTo("0,12,0,8"));
    }

    [Test]
    public void AnExplanation_IsCaptionInTheSecondaryBrush_NoWiderThan480()
    {
        XElement style = AppStyle("ExplanationText");

        Assert.That((SetterValue(style, "FontSize"), SetterValue(style, "MaxWidth"), SetterValue(style, "TextWrapping")),
            Is.EqualTo(("12", "480", "Wrap")));
    }

    [Test]
    public void TheUiFont_IsSegoeUiVariableText_FallingBackToSegoeUi_AndCodeUsesTheSameFace()
    {
        XElement font = AppXaml().Descendants(Presentation + "FontFamily").Single(each => (string?)each.Attribute(Xaml + "Key") == "UiFont");

        Assert.That(font.Value, Is.EqualTo("Segoe UI Variable Text, Segoe UI"));
        Assert.That(Theseus.App.Text.TypeRamp.UiFont.Source, Is.EqualTo(font.Value));
    }

    [Test]
    public void TheTypeRampConstants_MatchTheStyles()
    {
        Assert.That(
            new[] { Theseus.App.Text.TypeRamp.Caption, Theseus.App.Text.TypeRamp.Body, Theseus.App.Text.TypeRamp.BodyLarge,
                Theseus.App.Text.TypeRamp.Subtitle, Theseus.App.Text.TypeRamp.Title },
            Is.EqualTo(new double[] { 12, 14, 18, 20, 28 }));
    }

    [TestCase("Space4", "4")]
    [TestCase("Space8", "8")]
    [TestCase("Space12", "12")]
    [TestCase("Space16", "16")]
    [TestCase("Space24", "24")]
    [TestCase("DialogGutter", "24")]
    [TestCase("BoardGutter", "12")]
    [TestCase("SurfacePadding", "16")]
    [TestCase("ListRowPadding", "12,8")]
    [TestCase("ControlCornerRadius", "4")]
    [TestCase("OverlayCornerRadius", "8")]
    public void ASpacingOrRadiusToken_IsDefinedInAppXaml(string key, string value)
    {
        XElement token = AppXaml().Descendants().Single(each => (string?)each.Attribute(Xaml + "Key") == key);

        Assert.That(token.Value, Is.EqualTo(value));
    }

    [Test]
    public void TheAccentButton_DrawsItsLabelInTheTextOnAccentBrush_OnTheAccentFill()
    {
        Style accent = (Style)Shared()["AccentButton"];

        Assert.That(DynamicKey(StyleSetter(accent, "Foreground")), Is.EqualTo("TextOnAccentFillColorPrimaryBrush"));
        Assert.That(DynamicKey(StyleSetter(accent, "Background")), Is.EqualTo("AccentFillColorDefaultBrush"));
        Assert.That(StyleSetter(accent, "MinWidth"), Is.EqualTo(120.0), "W11");
    }

    [Test]
    public void TheDialogButton_IsACommandButtonAtLeast120Wide()
    {
        ResourceDictionary shared = Shared();
        Style dialog = (Style)shared["DialogButton"];

        Assert.That(dialog.BasedOn, Is.SameAs(shared["CommandButton"]));
        Assert.That(StyleSetter(dialog, "MinWidth"), Is.EqualTo(120.0));
    }

    [TestCase("CommandButton")]
    [TestCase("AccentButton")]
    [TestCase("DialogButton")]
    [TestCase("ToolButton")]
    [TestCase("ToolIcon")]
    [TestCase("ToolIconToggle")]
    [TestCase("ToggleSwitch")]
    public void EveryButtonStyle_IsAtLeast32High_AndToolButtons32Wide(string key)
    {
        Style style = (Style)Shared()[key];

        Assert.That(StyleSetter(style, "MinHeight"), Is.EqualTo(32.0), "W38, W56: one height of 32 in a compact row");

        if (key.StartsWith("Tool", StringComparison.Ordinal))
        {
            Assert.That(StyleSetter(style, "MinWidth"), Is.EqualTo(32.0));
            Assert.That(StyleSetter(style, "Width"), Is.Null, "no fixed width under the minimum");
            Assert.That(StyleSetter(style, "Height"), Is.Null, "no fixed height under the minimum");
        }
    }

    [TestCase("ToolButton", 16.0)]
    [TestCase("ToolIcon", 20.0)]
    public void AToolbarGlyph_UsesTheThemesIconFont_AtACrispSize(string key, double size)
    {
        Style style = (Style)Shared()[key];

        Assert.That(DynamicKey(StyleSetter(style, "FontFamily")), Is.EqualTo("SymbolThemeFontFamily"), "W24");
        Assert.That(StyleSetter(style, "FontSize"), Is.EqualTo(size), "W22");
    }

    [Test]
    public void HoverAndPressed_PutTextOnAccentOnTheAccent_NeverWhiteOrAFixedBlue()
    {
        string xaml = File.ReadAllText(SourceFile("Themes", "Interactive.xaml"));

        Assert.That(xaml, Does.Not.Contain("\"White\"").And.Not.Contain("#FF1F4E7A"), "E3, C5");
        Assert.That(xaml, Does.Not.Contain("Segoe MDL2 Assets\""), "W24: only as the fallback of the theme key");

        foreach (string key in new[] { "CommandButton", "ToolButton", "ChipButton" })
        {
            Style style = (Style)Shared()[key];
            ControlTemplate template = (ControlTemplate)StyleSetter(style, "Template")!;
            Trigger hover = template.Triggers.OfType<Trigger>().Single(trigger => trigger.Property.Name == "IsMouseOver");

            Assert.That(hover.Setters.OfType<Setter>().Where(setter => setter.Property.Name == "Foreground").Select(setter => DynamicKey(setter.Value)),
                Is.EqualTo(new[] { "TextOnAccentFillColorPrimaryBrush" }), key);
        }
    }

    [Test]
    public void TheToggleSwitch_IsAToggleButtonWithAnAccentTrackWhenOn()
    {
        Style style = (Style)Shared()["ToggleSwitch"];
        ControlTemplate template = (ControlTemplate)StyleSetter(style, "Template")!;
        Trigger on = template.Triggers.OfType<Trigger>().Single(trigger => trigger.Property.Name == "IsChecked" && Equals(trigger.Value, true));

        Assert.That(style.TargetType, Is.EqualTo(typeof(System.Windows.Controls.Primitives.ToggleButton)));
        Assert.That(on.Setters.OfType<Setter>().Where(setter => setter.TargetName == "Track" && setter.Property.Name == "Background")
            .Select(setter => DynamicKey(setter.Value)), Is.EqualTo(new[] { "AccentFillColorDefaultBrush" }));
    }

    private static ResourceDictionary Shared() =>
        Theseus.Tests.Conventions.LooseXaml.Dictionary("Themes", "Interactive.xaml");

    private static Window Host(FrameworkElement content)
    {
        Window window = new()
        {
            Content = content, Left = -30000, Top = 0, Width = 300, Height = 120, ShowActivated = false, ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };
        window.Show();
        window.UpdateLayout();

        return window;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);

            yield return child;

            foreach (DependencyObject below in Descendants(child))
            {
                yield return below;
            }
        }
    }

    private static object? StyleSetter(Style style, string property)
    {
        for (Style? each = style; each is not null; each = each.BasedOn)
        {
            if (each.Setters.OfType<Setter>().FirstOrDefault(setter => setter.Property.Name == property) is { } found)
            {
                return found.Value;
            }
        }

        return null;
    }

    private static object? DynamicKey(object? value) => (value as DynamicResourceExtension)?.ResourceKey;

    private static XDocument AppXaml() => XDocument.Load(SourceFile("App.xaml"));

    private static XElement AppStyle(string key) =>
        AppXaml().Descendants(Presentation + "Style").Single(style => (string?)style.Attribute(Xaml + "Key") == key);

    private static string? SetterValue(XElement style, string property) =>
        (string?)style.Elements(Presentation + "Setter").FirstOrDefault(setter => (string?)setter.Attribute("Property") == property)?.Attribute("Value");

    private static string SourceFile(params string[] path) =>
        Path.Combine([RepositoryRoot(), "src", "Theseus.App", .. path]);

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
