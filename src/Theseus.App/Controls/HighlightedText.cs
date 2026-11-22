using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Theseus.Core.Finding;

namespace Theseus.App.Controls;

/// <summary>
/// A TextBlock's text with the board search's matches highlighted in it (card-search-and-tags item 3). Set
/// <c>Text</c> and <c>Spans</c>; the TextBlock's runs are rebuilt whenever either changes, the matched runs in the
/// theme's search highlight.
/// </summary>
public static class HighlightedText
{
    /// <summary>The text to show.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(HighlightedText), new PropertyMetadata(string.Empty, OnChanged));

    /// <summary>The runs of it to highlight.</summary>
    public static readonly DependencyProperty SpansProperty = DependencyProperty.RegisterAttached(
        "Spans", typeof(IReadOnlyList<TextSpan>), typeof(HighlightedText), new PropertyMetadata(null, OnChanged));

    /// <summary>Gets the text.</summary>
    public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);

    /// <summary>Sets the text.</summary>
    public static void SetText(DependencyObject element, string value) => element.SetValue(TextProperty, value);

    /// <summary>Gets the runs to highlight.</summary>
    public static IReadOnlyList<TextSpan>? GetSpans(DependencyObject element) => (IReadOnlyList<TextSpan>?)element.GetValue(SpansProperty);

    /// <summary>Sets the runs to highlight.</summary>
    public static void SetSpans(DependencyObject element, IReadOnlyList<TextSpan>? value) => element.SetValue(SpansProperty, value);

    private static void OnChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBlock block)
        {
            return;
        }

        var text = GetText(block) ?? string.Empty;
        var spans = GetSpans(block) ?? [];

        // Plain text when nothing is highlighted, so Text reads as it always has; runs alone leave Text empty.
        if (spans.Count == 0)
        {
            block.Text = text;

            return;
        }

        System.Windows.Automation.AutomationProperties.SetName(block, text);
        block.Inlines.Clear();
        var at = 0;

        foreach (var span in spans.Where(span => span.Start >= at && span.Start + span.Length <= text.Length))
        {
            if (span.Start > at)
            {
                block.Inlines.Add(new Run(text[at..span.Start]));
            }

            var match = new Run(text.Substring(span.Start, span.Length));
            match.SetResourceReference(TextElement.BackgroundProperty, "SearchHighlightBrush");
            match.SetResourceReference(TextElement.ForegroundProperty, "SearchHighlightTextBrush");
            block.Inlines.Add(match);
            at = span.Start + span.Length;
        }

        if (at < text.Length)
        {
            block.Inlines.Add(new Run(text[at..]));
        }
    }
}
