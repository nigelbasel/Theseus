using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Theseus.App.Controls;

namespace Theseus.Tests.Conventions;

/// <summary>
/// The tool icon button's font is the icon font, and everything inside it inherits that. A word in it, as its content or
/// as a TextBlock, is drawn as empty boxes unless something between it and the button gives it the text font: a collapsed
/// kanban column's name was (2026-11-27). This reads every XAML file, so a button that is empty in a test is held too.
/// </summary>
[TestFixture]
public class ToolIconTextFontTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void NoWordInAToolIconButton_InheritsTheIconFont()
    {
        var documents = Documents().ToList();
        var styles = documents.SelectMany(document => document.Document.Descendants(Presentation + "Style")).ToList();
        var symbolStyles = SymbolStyles(styles);
        var textStyles = new HashSet<string>(styles.Where(style => SetsTextFont(style)).Select(Key).OfType<string>(), StringComparer.Ordinal);

        var faults = documents.SelectMany(document => document.Document.Descendants()
                .Where(element => IsSymbolButton(element, symbolStyles))
                .SelectMany(button => Words(button, textStyles).Select(word => $"{document.File}:{Line(word)} {Describe(word)}")))
            .Distinct().ToList();

        Assert.That(faults, Is.Empty, "Give each the text font, {DynamicResource UiFont}:" + Environment.NewLine + string.Join(Environment.NewLine, faults));
    }

    [Test]
    public void TheRule_FindsAWordInAToolIconButton_AndPassesOneGivenTheTextFont()
    {
        var symbolStyles = new HashSet<string>(["ToolIcon"], StringComparer.Ordinal);
        var faulty = XElement.Parse($$"""
            <Button xmlns="{{Presentation}}" Style="{StaticResource ToolIcon}"><StackPanel><TextBlock Text="{Binding Name}" /></StackPanel></Button>
            """, LoadOptions.SetLineInfo);
        var fixedOne = XElement.Parse($$"""
            <Button xmlns="{{Presentation}}" Style="{StaticResource ToolIcon}"><StackPanel><TextBlock Text="{Binding Name}" FontFamily="{DynamicResource UiFont}" /></StackPanel></Button>
            """, LoadOptions.SetLineInfo);

        Assert.That((IsSymbolButton(faulty, symbolStyles), Words(faulty, []).Count(), Words(fixedOne, []).Count()), Is.EqualTo((true, 1, 0)));
    }

    // Styles whose font is the icon font: set so, or based on one that is without setting another.
    private static HashSet<string> SymbolStyles(List<XElement> styles)
    {
        var symbol = new HashSet<string>(StringComparer.Ordinal);
        bool grew;

        do
        {
            grew = false;

            foreach (var style in styles)
            {
                if (Key(style) is { } key && !symbol.Contains(key) && IsSymbolStyle(style, symbol))
                {
                    symbol.Add(key);
                    grew = true;
                }
            }
        }
        while (grew);

        return symbol;
    }

    private static bool IsSymbolStyle(XElement style, HashSet<string> symbolStyles)
    {
        if (FontSetters(style).FirstOrDefault() is { } font)
        {
            return IsSymbolFont(font);
        }

        return Referenced((string?)style.Attribute("BasedOn")) is { } basis && symbolStyles.Contains(basis);
    }

    private static bool IsSymbolButton(XElement element, HashSet<string> symbolStyles)
    {
        if (element.Name.Namespace != Presentation || element.Name.LocalName is not ("Button" or "ToggleButton" or "RepeatButton"))
        {
            return false;
        }

        if (OwnFont(element) is { } own)
        {
            return IsSymbolFont(own);
        }

        if (Referenced((string?)element.Attribute("Style")) is { } key)
        {
            return symbolStyles.Contains(key);
        }

        return element.Elements(Presentation + (element.Name.LocalName + ".Style")).Elements(Presentation + "Style")
            .Any(style => IsSymbolStyle(style, symbolStyles));
    }

    // Words the button draws in its own font: string content, and TextBlocks with nothing between them and it to change it.
    private static IEnumerable<XElement> Words(XElement button, HashSet<string> textStyles)
    {
        if ((string?)button.Attribute("Content") is { } content && !content.StartsWith('{') && !GlyphTemplateSelector.IsGlyph(content) && content.Length > 0)
        {
            yield return button;
        }

        foreach (var block in button.Descendants(Presentation + "TextBlock"))
        {
            if ((string?)block.Attribute("Text") is { } text && !text.StartsWith('{') && GlyphTemplateSelector.IsGlyph(text))
            {
                continue;
            }

            if (Referenced((string?)block.Attribute("Style")) is { } style && textStyles.Contains(style))
            {
                continue;
            }

            if (!block.AncestorsAndSelf().TakeWhile(each => each != button).Any(GivesTextFont))
            {
                yield return block;
            }
        }
    }

    // An element that sets the text font itself, or a style or trigger whose setters do (the setter's template sits inside it).
    private static bool GivesTextFont(XElement element) =>
        OwnFont(element) is { } own ? !IsSymbolFont(own) : SetsTextFont(element);

    private static bool SetsTextFont(XElement element) => FontSetters(element).Any(value => !IsSymbolFont(value));

    private static IEnumerable<string> FontSetters(XElement element) =>
        element.Elements(Presentation + "Setter")
            .Where(setter => (string?)setter.Attribute("Property") is "FontFamily" or "TextElement.FontFamily" or "TextBlock.FontFamily" or "Control.FontFamily")
            .Select(setter => (string?)setter.Attribute("Value") ?? string.Empty);

    private static string? OwnFont(XElement element) =>
        (string?)element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName is "FontFamily" or "TextElement.FontFamily");

    private static bool IsSymbolFont(string value) =>
        value.Contains("SymbolThemeFontFamily", StringComparison.Ordinal) || value.Contains("Icons", StringComparison.Ordinal)
        || value.Contains("MDL2", StringComparison.Ordinal);

    private static string? Key(XElement style) =>
        (string?)style.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Key");

    private static string Describe(XElement word) =>
        word.Name.LocalName == "TextBlock"
            ? $"TextBlock {(string?)word.Attribute("Text") ?? "(runs)"}"
            : $"{word.Name.LocalName} Content=\"{(string?)word.Attribute("Content")}\"";

    private static int Line(XElement element) => ((IXmlLineInfo)element).LineNumber;

    private static string? Referenced(string? markup)
    {
        var match = Regex.Match(markup ?? string.Empty, @"^\{(?:StaticResource|DynamicResource)\s+(?:ResourceKey=)?([\w.]+)\}$");

        return match.Success ? match.Groups[1].Value : null;
    }

    private static IEnumerable<(string File, XDocument Document)> Documents() =>
        Directory.EnumerateFiles(AppRoot(), "*.xaml", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(AppRoot(), path).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(path => !path.StartsWith("bin/", StringComparison.Ordinal) && !path.StartsWith("obj/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(path => (path, XDocument.Load(Path.Combine(AppRoot(), path), LoadOptions.SetLineInfo)));

    private static string AppRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", "..", "src", "Theseus.App"));
}
