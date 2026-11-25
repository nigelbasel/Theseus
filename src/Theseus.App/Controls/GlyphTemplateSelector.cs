using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Theseus.App.Controls;

/// <summary>
/// Draws a button's content with <see cref="GlyphText"/> when it is an icon-font glyph, a string of Private Use Area
/// characters (baseline W50, E10), so screen readers skip it. Any other content, words included, takes the presenter's
/// own template, so a word is still read and still carries its access key.
/// </summary>
public sealed class GlyphTemplateSelector : DataTemplateSelector
{
    private static readonly DataTemplate Glyph = GlyphTemplate();

    /// <summary>The one instance the shared templates use.</summary>
    public static GlyphTemplateSelector Instance { get; } = new();

    /// <summary>Whether the text is only icon-font glyphs.</summary>
    public static bool IsGlyph(string? text) => !string.IsNullOrEmpty(text) && text.All(character => character is >= '' and <= '');

    /// <inheritdoc />
    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) => item is string text && IsGlyph(text) ? Glyph : null;

    private static DataTemplate GlyphTemplate()
    {
        FrameworkElementFactory glyph = new(typeof(GlyphText));
        glyph.SetBinding(TextBlock.TextProperty, new Binding());
        DataTemplate template = new() { VisualTree = glyph };
        template.Seal();

        return template;
    }
}
