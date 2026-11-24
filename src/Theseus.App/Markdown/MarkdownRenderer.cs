using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Theseus.App.Text;
using Theseus.Core.Markdown;

namespace Theseus.App.Markdown;

/// <summary>
/// Draws Markdown natively in WPF, without a browser engine or a new package (1.1e AC 23): every colour is a theme
/// resource reference, so light and dark follow the app.
/// </summary>
public static class MarkdownRenderer
{
    /// <summary>The monospace face for code.</summary>
    public static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Courier New");

    /// <summary>A document of the text's blocks.</summary>
    public static FlowDocument Render(string? text)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(4),
            MaxPageWidth = 640,
            FontFamily = TypeRamp.UiFont,
            FontSize = TypeRamp.Body,
            TextAlignment = TextAlignment.Left,
        };
        document.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorPrimaryBrush");
        List? list = null;
        var listKind = (MarkdownBlockKind?)null;
        var listDepth = -1;

        foreach (var block in MarkdownParser.Blocks(text))
        {
            var isItem = block.Kind is MarkdownBlockKind.Bullet or MarkdownBlockKind.Numbered or MarkdownBlockKind.Task;

            if (!isItem)
            {
                list = null;
                listKind = null;
                listDepth = -1;
                document.Blocks.Add(BlockOf(block));

                continue;
            }

            var kind = block.Kind == MarkdownBlockKind.Task ? MarkdownBlockKind.Bullet : block.Kind;

            if (list is null || listKind != kind || listDepth != block.Depth)
            {
                list = new List
                {
                    MarkerStyle = block.Kind switch
                    {
                        MarkdownBlockKind.Numbered => TextMarkerStyle.Decimal,
                        MarkdownBlockKind.Task => TextMarkerStyle.None,
                        _ => block.Depth == 0 ? TextMarkerStyle.Disc : TextMarkerStyle.Circle,
                    },
                    StartIndex = block.Kind == MarkdownBlockKind.Numbered ? Math.Max(1, block.Number) : 1,
                    Margin = new Thickness(20 * (block.Depth + 1), 4, 0, 4),
                    Padding = new Thickness(0),
                };
                listKind = kind;
                listDepth = block.Depth;
                document.Blocks.Add(list);
            }

            Paragraph paragraph = new() { Margin = new Thickness(0, 0, 0, 4) };

            if (block.Kind == MarkdownBlockKind.Task)
            {
                paragraph.Inlines.Add(new Run(block.IsChecked ? "☑ " : "☐ ") { FontFamily = new FontFamily("Segoe UI Symbol") });
            }

            AddInlines(paragraph.Inlines, block.Text);
            list.ListItems.Add(new ListItem(paragraph));
        }

        return document;
    }

    /// <summary>Opens a link's web address in the browser; any other address is left alone.</summary>
    public static void Follow(Uri? address)
    {
        if (address is { IsAbsoluteUri: true } && (address.Scheme == Uri.UriSchemeHttps || address.Scheme == Uri.UriSchemeHttp))
        {
            Process.Start(new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
        }
    }

    private static Block BlockOf(MarkdownBlock block)
    {
        switch (block.Kind)
        {
            case MarkdownBlockKind.Heading:
            {
                Paragraph heading = new()
                {
                    FontSize = block.Level switch { 1 => TypeRamp.Subtitle, 2 => TypeRamp.BodyLarge, _ => TypeRamp.Body },
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, block.Level <= 2 ? 16 : 12, 0, 4),
                };
                AddInlines(heading.Inlines, block.Text);

                return heading;
            }

            case MarkdownBlockKind.Code:
            {
                Paragraph code = new(new Run(block.Text))
                {
                    FontFamily = Mono,
                    FontSize = TypeRamp.Caption,
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(0, 4, 0, 8),
                    BorderThickness = new Thickness(1),
                };
                code.SetResourceReference(TextElement.BackgroundProperty, "ControlFillColorSecondaryBrush");
                code.SetResourceReference(Block.BorderBrushProperty, "CardStrokeColorDefaultBrush");

                return code;
            }

            case MarkdownBlockKind.Quote:
            {
                Paragraph quote = new()
                {
                    Padding = new Thickness(12, 4, 0, 4),
                    Margin = new Thickness(0, 4, 0, 8),
                    BorderThickness = new Thickness(3, 0, 0, 0),
                };
                quote.SetResourceReference(Block.BorderBrushProperty, "AccentFillColorDefaultBrush");
                quote.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorSecondaryBrush");
                AddInlines(quote.Inlines, block.Text);

                return quote;
            }

            case MarkdownBlockKind.Rule:
            {
                Paragraph rule = new() { Margin = new Thickness(0, 8, 0, 8), BorderThickness = new Thickness(0, 0, 0, 1), FontSize = 2 };
                rule.SetResourceReference(Block.BorderBrushProperty, "CardStrokeColorDefaultBrush");

                return rule;
            }

            case MarkdownBlockKind.Table:
                return TableOf(block);

            default:
            {
                Paragraph paragraph = new() { Margin = new Thickness(0, 4, 0, 8) };
                AddInlines(paragraph.Inlines, block.Text);

                return paragraph;
            }
        }
    }

    private static Table TableOf(MarkdownBlock block)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 4, 0, 8), BorderThickness = new Thickness(1, 1, 0, 0) };
        table.SetResourceReference(Block.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        var columns = block.Rows.Max(row => row.Count);

        for (var column = 0; column < columns; column++)
        {
            table.Columns.Add(new TableColumn { Width = GridLength.Auto });
        }

        var group = new TableRowGroup();
        table.RowGroups.Add(group);

        foreach (var (cells, index) in block.Rows.Select((row, index) => (row, index)))
        {
            var row = new TableRow();

            for (var column = 0; column < columns; column++)
            {
                var paragraph = new Paragraph { Margin = new Thickness(0) };
                AddInlines(paragraph.Inlines, column < cells.Count ? cells[column] : string.Empty);
                TableCell cell = new(paragraph) { Padding = new Thickness(8, 4, 8, 4), BorderThickness = new Thickness(0, 0, 1, 1) };
                cell.SetResourceReference(TableCell.BorderBrushProperty, "CardStrokeColorDefaultBrush");

                if (index == 0)
                {
                    cell.FontWeight = FontWeights.SemiBold;
                    cell.SetResourceReference(TableCell.BackgroundProperty, "ControlFillColorSecondaryBrush");
                }

                row.Cells.Add(cell);
            }

            group.Rows.Add(row);
        }

        return table;
    }

    private static void AddInlines(InlineCollection inlines, string text)
    {
        foreach (var span in MarkdownParser.Inlines(text))
        {
            switch (span.Kind)
            {
                case MarkdownInlineKind.Strong:
                {
                    // What is inside bold or italic is read again, so code or a link in it is drawn (review of PR 5, 7).
                    var bold = new Bold();
                    AddInlines(bold.Inlines, span.Text);
                    inlines.Add(bold);

                    break;
                }

                case MarkdownInlineKind.Emphasis:
                {
                    var italic = new Italic();
                    AddInlines(italic.Inlines, span.Text);
                    inlines.Add(italic);

                    break;
                }
                case MarkdownInlineKind.Code:
                {
                    Run code = new(span.Text) { FontFamily = Mono, FontSize = TypeRamp.Caption };
                    code.SetResourceReference(TextElement.BackgroundProperty, "ControlFillColorSecondaryBrush");
                    inlines.Add(code);

                    break;
                }

                case MarkdownInlineKind.Link:
                {
                    var link = new Hyperlink(new Run(span.Text)) { ToolTip = span.Url };

                    if (Uri.TryCreate(span.Url, UriKind.Absolute, out var address))
                    {
                        link.NavigateUri = address;
                    }

                    link.SetResourceReference(TextElement.ForegroundProperty, "AccentTextFillColorPrimaryBrush");
                    link.RequestNavigate += (_, e) =>
                    {
                        Follow(e.Uri);
                        e.Handled = true;
                    };
                    inlines.Add(link);

                    break;
                }

                default:
                    inlines.Add(new Run(span.Text));

                    break;
            }
        }
    }
}
