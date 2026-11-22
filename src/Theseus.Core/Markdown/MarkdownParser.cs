using System.Text;
using System.Text.RegularExpressions;

namespace Theseus.Core.Markdown;

/// <summary>
/// Reads the Markdown the preview draws (1.1e AC 23): headings, emphasis, lists and check boxes, inline code and code
/// blocks, links, quotes and tables. Not every corner of CommonMark: what Theseus's own prompts and specs use.
/// </summary>
public static partial class MarkdownParser
{
    /// <summary>The blocks of a text, in order.</summary>
    public static IReadOnlyList<MarkdownBlock> Blocks(string? text)
    {
        var lines = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var blocks = new List<MarkdownBlock>();
        var paragraph = new List<string>();
        var index = 0;

        void EndParagraph()
        {
            if (paragraph.Count > 0)
            {
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Paragraph, string.Join(" ", paragraph)));
                paragraph.Clear();
            }
        }

        while (index < lines.Length)
        {
            var line = lines[index];

            if (string.IsNullOrWhiteSpace(line))
            {
                EndParagraph();
                index++;

                continue;
            }

            if (Fence().Match(line) is { Success: true } fence)
            {
                EndParagraph();
                var code = new List<string>();
                index++;

                while (index < lines.Length && !lines[index].TrimStart().StartsWith(fence.Groups[1].Value, StringComparison.Ordinal))
                {
                    code.Add(lines[index]);
                    index++;
                }

                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Code, string.Join("\n", code)));
                index++;

                continue;
            }

            if (Heading().Match(line) is { Success: true } heading)
            {
                EndParagraph();
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Heading, heading.Groups[2].Value.Trim().TrimEnd('#').TrimEnd())
                {
                    Level = heading.Groups[1].Length,
                });
                index++;

                continue;
            }

            if (Rule().IsMatch(line))
            {
                EndParagraph();
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Rule, string.Empty));
                index++;

                continue;
            }

            if (line.TrimStart().StartsWith('|') && index + 1 < lines.Length && TableSeparator().IsMatch(lines[index + 1]))
            {
                EndParagraph();
                var rows = new List<IReadOnlyList<string>> { Cells(line) };
                index += 2;

                while (index < lines.Length && lines[index].TrimStart().StartsWith('|'))
                {
                    rows.Add(Cells(lines[index]));
                    index++;
                }

                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Table, string.Empty) { Rows = rows });

                continue;
            }

            if (Quote().Match(line) is { Success: true })
            {
                EndParagraph();
                var quoted = new List<string>();

                while (index < lines.Length && Quote().Match(lines[index]) is { Success: true } more)
                {
                    quoted.Add(more.Groups[1].Value);
                    index++;
                }

                blocks.Add(new MarkdownBlock(MarkdownBlockKind.Quote, string.Join(" ", quoted.Where(each => each.Length > 0))));

                continue;
            }

            if (ListItem(line) is { } item)
            {
                EndParagraph();
                index++;
                var carried = new StringBuilder(item.Text);

                // A line indented under an item, with no marker of its own, carries on its text.
                while (index < lines.Length && lines[index].Length > 0 && char.IsWhiteSpace(lines[index][0])
                       && !string.IsNullOrWhiteSpace(lines[index]) && ListItem(lines[index]) is null
                       && !Fence().IsMatch(lines[index]) && !Heading().IsMatch(lines[index]) && !Quote().IsMatch(lines[index])
                       && !lines[index].TrimStart().StartsWith('|'))
                {
                    carried.Append(' ').Append(lines[index].Trim());
                    index++;
                }

                blocks.Add(item with { Text = carried.ToString() });

                continue;
            }

            paragraph.Add(line.Trim());
            index++;
        }

        EndParagraph();

        return blocks;
    }

    /// <summary>The inline spans of a block's text, in order.</summary>
    public static IReadOnlyList<MarkdownInline> Inlines(string? text)
    {
        var spans = new List<MarkdownInline>();
        var plain = new StringBuilder();
        var source = text ?? string.Empty;
        var index = 0;

        void Add(MarkdownInline span)
        {
            if (plain.Length > 0)
            {
                spans.Add(new MarkdownInline(MarkdownInlineKind.Text, plain.ToString()));
                plain.Clear();
            }

            spans.Add(span);
        }

        while (index < source.Length)
        {
            var rest = source.AsSpan(index);
            var at = source[index];

            if (at == '\\' && index + 1 < source.Length && (char.IsPunctuation(source[index + 1]) || char.IsSymbol(source[index + 1])))
            {
                plain.Append(source[index + 1]);
                index += 2;

                continue;
            }

            if (at == '`' && source.IndexOf('`', index + 1) is var close and > 0)
            {
                Add(new MarkdownInline(MarkdownInlineKind.Code, source[(index + 1)..close]));
                index = close + 1;

                continue;
            }

            if ((rest.StartsWith("**") || rest.StartsWith("__")) && source.IndexOf(source.Substring(index, 2), index + 2, StringComparison.Ordinal) is var end and > 0
                && end > index + 2)
            {
                Add(new MarkdownInline(MarkdownInlineKind.Strong, source[(index + 2)..end]));
                index = end + 2;

                continue;
            }

            if ((at == '*' || (at == '_' && (index == 0 || !char.IsLetterOrDigit(source[index - 1]))))
                && index + 1 < source.Length && !char.IsWhiteSpace(source[index + 1])
                && source.IndexOf(at, index + 1) is var stop and > 0 && stop > index + 1 && !char.IsWhiteSpace(source[stop - 1])
                && (at != '_' || stop + 1 >= source.Length || !char.IsLetterOrDigit(source[stop + 1])))
            {
                Add(new MarkdownInline(MarkdownInlineKind.Emphasis, source[(index + 1)..stop]));
                index = stop + 1;

                continue;
            }

            if (at == '[' && Link().Match(source, index) is { Success: true } link && link.Index == index)
            {
                Add(new MarkdownInline(MarkdownInlineKind.Link, link.Groups[1].Value, link.Groups[2].Value));
                index += link.Length;

                continue;
            }

            if ((at == 'h' || at == '<') && BareUrl().Match(source, index) is { Success: true } url && url.Index == index
                && (index == 0 || !char.IsLetterOrDigit(source[index - 1])))
            {
                var address = url.Groups[1].Value.TrimEnd('.', ',', ';', ':', ')');
                Add(new MarkdownInline(MarkdownInlineKind.Link, address, address));
                index += at == '<' ? url.Length : address.Length;

                continue;
            }

            plain.Append(at);
            index++;
        }

        if (plain.Length > 0)
        {
            spans.Add(new MarkdownInline(MarkdownInlineKind.Text, plain.ToString()));
        }

        return spans;
    }

    private static MarkdownBlock? ListItem(string line)
    {
        if (TaskItem().Match(line) is { Success: true } task)
        {
            return new MarkdownBlock(MarkdownBlockKind.Task, task.Groups[3].Value)
            {
                Depth = task.Groups[1].Value.Replace("\t", "  ", StringComparison.Ordinal).Length / 2,
                IsChecked = task.Groups[2].Value is "x" or "X",
            };
        }

        if (Bullet().Match(line) is { Success: true } bullet)
        {
            return new MarkdownBlock(MarkdownBlockKind.Bullet, bullet.Groups[2].Value)
            {
                Depth = bullet.Groups[1].Value.Replace("\t", "  ", StringComparison.Ordinal).Length / 2,
            };
        }

        if (Numbered().Match(line) is { Success: true } numbered)
        {
            return new MarkdownBlock(MarkdownBlockKind.Numbered, numbered.Groups[3].Value)
            {
                Depth = numbered.Groups[1].Value.Replace("\t", "  ", StringComparison.Ordinal).Length / 2,
                Number = int.TryParse(numbered.Groups[2].Value, out var number) ? number : 1,
            };
        }

        return null;
    }

    private static List<string> Cells(string line)
    {
        var trimmed = line.Trim();
        trimmed = trimmed.StartsWith('|') ? trimmed[1..] : trimmed;
        trimmed = trimmed.EndsWith('|') && !trimmed.EndsWith("\\|", StringComparison.Ordinal) ? trimmed[..^1] : trimmed;

        return [.. CellSplit().Split(trimmed).Select(cell => cell.Replace("\\|", "|", StringComparison.Ordinal).Trim())];
    }

    [GeneratedRegex(@"^\s*(```|~~~)")]
    private static partial Regex Fence();

    [GeneratedRegex(@"^\s{0,3}(#{1,6})\s+(.*)$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^\s{0,3}([-*_])(\s*\1){2,}\s*$")]
    private static partial Regex Rule();

    [GeneratedRegex(@"^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)+\|?\s*$|^\s*\|\s*:?-+:?\s*\|\s*$")]
    private static partial Regex TableSeparator();

    [GeneratedRegex(@"^\s{0,3}>\s?(.*)$")]
    private static partial Regex Quote();

    [GeneratedRegex(@"^(\s*)[-*+]\s+\[([ xX])\]\s+(.*)$")]
    private static partial Regex TaskItem();

    [GeneratedRegex(@"^(\s*)[-*+]\s+(.*)$")]
    private static partial Regex Bullet();

    [GeneratedRegex(@"^(\s*)(\d{1,9})[.)]\s+(.*)$")]
    private static partial Regex Numbered();

    // Anchored with \G at the point scanned from, so a scan of a long text is not quadratic (review of PR 5, finding 8).
    [GeneratedRegex(@"\G\[([^\]]+)\]\(([^)\s]+)\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"\G<?(https?://[^\s<>]+)>?")]
    private static partial Regex BareUrl();

    [GeneratedRegex(@"(?<!\\)\|")]
    private static partial Regex CellSplit();
}
