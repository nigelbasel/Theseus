using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// Everything clickable answers the pointer and the keyboard (1.1e AC 6b): the shared styles in Themes/Interactive.xaml
/// give every kind of clickable thing a hover, a pressed state, a disabled look and a keyboard-focus ring, and links and
/// chips the hand cursor and an underline on hover. A disabled button greys its label and drops its fill rather than
/// fading the enabled face (1.1e AC 6a).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class InteractiveStylesTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly string[] SharedKeys =
        ["CommandButton", "ToolButton", "ToolIconTemplate", "ToolIcon", "ToolIconToggle", "ChipButton", "LinkButton",
         "ClickableSurface", "SurfaceWash", "ClickableCard", "FocusRing", "CardFocusRing"];

    private static ResourceDictionary Shared() =>
        Theseus.Tests.Conventions.LooseXaml.Dictionary("Themes", "Interactive.xaml");

    private static Style StyleOf(string key) => (Style)Shared()[key];

    [Test]
    public void TheSharedStyles_AreDefinedInInteractive_AndInNoWindow()
    {
        var definitions = Directory.EnumerateFiles(SourceFile(), "*.xaml", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => XDocument.Load(file).Descendants().Select(element => (file, key: (string?)element.Attribute(Xaml + "Key"))))
            .Where(each => each.key is not null && SharedKeys.Contains(each.key))
            .Select(each => (Path.GetFileName(each.file), each.key!))
            .ToList();

        Assert.That(definitions, Is.EquivalentTo(SharedKeys.Select(key => ("Interactive.xaml", key))));
    }

    [Test]
    public void TheCommandButton_AnswersHoverPressAndFocus_AndWhenDisabled_LosesItsFillAndGreysItsLabel()
    {
        var style = StyleOf("CommandButton");
        var triggers = Triggers(style);
        var disabled = PropertyTrigger(triggers, "IsEnabled", false);

        Assert.That(FocusVisual(style), Is.Not.Null, "a keyboard-focus ring");
        Assert.That(Sets(PropertyTrigger(triggers, "IsMouseOver", true), "Glow", "Opacity"), Is.EqualTo(1.0));
        Assert.That(PropertyTrigger(triggers, "IsPressed", true), Is.Not.Null);
        Assert.That((Sets(disabled, "Fill", "Background")?.ToString(), ResourceKey(Sets(disabled, "Fill", "BorderBrush")),
                ResourceKey(Sets(disabled, "Label", "Foreground")), Sets(disabled, "Glow", "Opacity")),
            Is.EqualTo(("#00FFFFFF", "CardStrokeColorDefaultBrush", "TextFillColorDisabledBrush", (object)0.0)),
            "no fill, a faint outline and grey text: unmistakably not a button for now (1.1e AC 6a)");
        Assert.That(triggers.OfType<Trigger>().Last(), Is.SameAs(disabled), "disabled is last, so it wins over hover");
    }

    [TestCase("ToolButton")]
    [TestCase("ToolIcon")]
    [TestCase("ToolIconToggle")]
    public void AToolbarButton_AnswersHoverPressAndFocus_AndGreysItsGlyphWhenDisabled(string key)
    {
        var style = StyleOf(key);
        var triggers = Triggers(style);

        Assert.That(FocusVisual(style), Is.Not.Null, "a keyboard-focus ring");
        Assert.That(PropertyTrigger(triggers, "IsMouseOver", true), Is.Not.Null);
        Assert.That(PropertyTrigger(triggers, "IsPressed", true), Is.Not.Null);
        Assert.That(ResourceKey(Sets(PropertyTrigger(triggers, "IsEnabled", false), "Label", "Foreground")), Is.EqualTo("TextFillColorDisabledBrush"));
    }

    [Test]
    public void AChip_HasTheHandCursor_IsUnderlinedOnHover_AnswersPressAndFocus_AndLooksDisabled()
    {
        var style = StyleOf("ChipButton");
        var triggers = Triggers(style);
        var disabled = PropertyTrigger(triggers, "IsEnabled", false);

        Assert.That(Setter(style, "Cursor")?.Value?.ToString(), Is.EqualTo("Hand"));
        Assert.That(FocusVisual(style), Is.Not.Null, "a keyboard-focus ring");
        Assert.That(ResourceKey(Sets(PropertyTrigger(triggers, "IsMouseOver", true), "Underline", "Fill")), Is.EqualTo("TextOnAccentFillColorPrimaryBrush"),
            "on the accent, in the text-on-accent brush (baseline E3)");
        Assert.That(PropertyTrigger(triggers, "IsPressed", true), Is.Not.Null);
        Assert.That((ResourceKey(Sets(disabled, "Chip", "Background")), ResourceKey(Sets(disabled, "Label", "Foreground"))),
            Is.EqualTo(("ControlFillColorDisabledBrush", "TextFillColorDisabledBrush")));
    }

    [Test]
    public void ALink_HasTheHandCursor_IsUnderlinedOnHover_AnswersPressAndFocus_AndLooksDisabled()
    {
        var style = StyleOf("LinkButton");
        var triggers = Triggers(style);

        Assert.That(Setter(style, "Cursor")?.Value?.ToString(), Is.EqualTo("Hand"));
        Assert.That(FocusVisual(style), Is.Not.Null, "a keyboard-focus ring");
        Assert.That(Sets(PropertyTrigger(triggers, "IsMouseOver", true), "Underline", "BorderBrush"), Is.Not.Null);
        Assert.That(PropertyTrigger(triggers, "IsPressed", true), Is.Not.Null);
        Assert.That(ResourceKey(Sets(PropertyTrigger(triggers, "IsEnabled", false), "Text", "Foreground")), Is.EqualTo("TextFillColorDisabledBrush"));
    }

    [Test]
    public void ATabAndAStageOfAChain_TakeTheSharedSurface_ItsRing_AndItsWashForHoverPressAndDisabled()
    {
        var surface = StyleOf("ClickableSurface");
        var wash = StyleOf("SurfaceWash").Triggers.OfType<DataTrigger>().ToList();
        var view = XDocument.Load(SourceFile("Views", "ProjectView.xaml"));

        Assert.That(FocusVisual(surface), Is.Not.Null, "a keyboard-focus ring");
        Assert.That(ResourceKey(PropertyTrigger(surface.Triggers.ToList(), "IsEnabled", false)!.Setters.OfType<Setter>().Single().Value),
            Is.EqualTo("TextFillColorDisabledBrush"));
        Assert.That(wash.Select(trigger => (((Binding)trigger.Binding).Path.Path, trigger.Value?.ToString())),
            Is.EqualTo(new[] { ("IsMouseOver", "True"), ("IsPressed", "True"), ("IsEnabled", "False") }));

        foreach (var key in new[] { "ProjectTab", "ChainStage" })
        {
            var style = view.Descendants(Presentation + "Style").Single(each => (string?)each.Attribute(Xaml + "Key") == key);

            Assert.That((string?)style.Attribute("BasedOn"), Is.EqualTo("{StaticResource ClickableSurface}"), key);
            Assert.That(style.Descendants(Presentation + "Border").Select(border => (string?)border.Attribute("Style")),
                Has.One.EqualTo("{StaticResource SurfaceWash}"), key);
        }
    }

    [Test]
    public void AClickableCard_StrengthensItsBorderOnHover_AndTakesARingOfItsShape_OnBothKindsOfCard()
    {
        var shared = Shared();
        var card = (Style)shared["ClickableCard"];
        var window = XDocument.Load(SourceFile("MainWindow.xaml"));

        Assert.That(FocusVisual(card), Is.SameAs(shared["CardFocusRing"]), "a ring of the card's own corners");
        Assert.That(ResourceKey(PropertyTrigger(card.Triggers.ToList(), "IsMouseOver", true)!.Setters.OfType<Setter>().Single().Value),
            Is.EqualTo("ControlStrongStrokeColorDefaultBrush"));
        Assert.That(PropertyTrigger(card.Triggers.ToList(), "IsEnabled", false), Is.Not.Null);

        foreach (var name in new[] { "CardFace", "ProjectFace" })
        {
            var face = window.Descendants(Presentation + "Border").Single(border => (string?)border.Attribute(Xaml + "Name") == name);

            Assert.That((string?)face.Element(Presentation + "Border.Style")?.Element(Presentation + "Style")?.Attribute("BasedOn"),
                Is.EqualTo("{StaticResource ClickableCard}"), name);
            Assert.That(face.Attribute("FocusVisualStyle"), Is.Null, $"{name} keeps the shared ring");
        }
    }

    private static List<TriggerBase> Triggers(Style style)
    {
        var all = new List<TriggerBase>();

        for (var each = style; each is not null; each = each.BasedOn)
        {
            all.InsertRange(0, each.Triggers);

            if (Setter(each, "Template")?.Value is ControlTemplate template)
            {
                all.AddRange(template.Triggers);
            }
        }

        return all;
    }

    private static Trigger? PropertyTrigger(IEnumerable<TriggerBase> triggers, string property, bool value) =>
        triggers.OfType<Trigger>().FirstOrDefault(trigger => trigger.Property.Name == property && Equals(trigger.Value, value));

    private static object? Sets(Trigger? trigger, string target, string property) =>
        trigger?.Setters.OfType<Setter>().FirstOrDefault(setter => setter.TargetName == target && setter.Property.Name == property)?.Value;

    private static Setter? Setter(Style style, string property)
    {
        for (var each = style; each is not null; each = each.BasedOn)
        {
            if (each.Setters.OfType<Setter>().FirstOrDefault(setter => setter.Property.Name == property) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static Style? FocusVisual(Style style) => Setter(style, nameof(FrameworkElement.FocusVisualStyle))?.Value as Style;

    private static object? ResourceKey(object? value) => (value as DynamicResourceExtension)?.ResourceKey;

    private static string SourceFile(params string[] path) =>
        Path.Combine([RepositoryRoot(), "src", "Theseus.App", .. path]);

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
