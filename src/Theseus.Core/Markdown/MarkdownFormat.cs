using System.Globalization;
using System.Text.RegularExpressions;

namespace Theseus.Core.Markdown;

/// <summary>
/// The Markdown box's toolbar (1.1e AC 22): each button only inserts Markdown, so the text saved is exactly what is typed.
/// A button pressed again on what it made takes it off again.
/// </summary>
public static partial class MarkdownFormat
{
    /// <summary>What a button does to a text with a selection (or just a caret, when the length is 0).</summary>
    public static MarkdownChange Apply(MarkdownAction action, string text, int selectionStart, int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var length = Math.Clamp(selectionLength, 0, text.Length - start);

        return action switch
        {
            MarkdownAction.Bold => Wrap(text, start, length, "**", "bold text"),
            MarkdownAction.Italic => Wrap(text, start, length, "*", "italic text"),
            MarkdownAction.Code when text.AsSpan(start, length).Contains('\n') => Fence(text, start, length),
            MarkdownAction.Code => Wrap(text, start, length, "`", "code"),
            MarkdownAction.Link => Link(text, start, length),
            MarkdownAction.Heading => Lines(text, start, length, _ => "## ", HeadingMark()),
            MarkdownAction.BulletedList => Lines(text, start, length, _ => "- ", BulletMark()),
            MarkdownAction.NumberedList => Lines(text, start, length,
                index => $"{(index + 1).ToString(CultureInfo.InvariantCulture)}. ", NumberMark()),
            MarkdownAction.CheckBox => Lines(text, start, length, _ => "- [ ] ", CheckMark()),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };
    }

    /// <summary>
    /// One criterion from a line of Add objective's box: trimmed, without a list or check box mark the toolbar put there,
    /// since the project file gives each criterion its own check box.
    /// </summary>
    public static string WithoutListMark(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return AnyMark().Replace(line.Trim(), string.Empty, 1).Trim();
    }

    // Around the selection, or a placeholder selected when there is none; off again when the selection is already wrapped.
    private static MarkdownChange Wrap(string text, int start, int length, string mark, string placeholder)
    {
        var selected = text.Substring(start, length);

        // A selected **word** is bold, not italic wrapped in asterisks, so Italic wraps it rather than unwrapping one star.
        var isBoldForItalic = mark == "*" && selected.StartsWith("**", StringComparison.Ordinal) && !selected.StartsWith("***", StringComparison.Ordinal);

        if (!isBoldForItalic && length >= mark.Length * 2 && selected.StartsWith(mark, StringComparison.Ordinal)
            && selected.EndsWith(mark, StringComparison.Ordinal))
        {
            var inner = selected[mark.Length..^mark.Length];

            return new MarkdownChange(start, length, inner, start, inner.Length);
        }

        if (start >= mark.Length && start + length + mark.Length <= text.Length
            && text.AsSpan(start - mark.Length, mark.Length).SequenceEqual(mark)
            && text.AsSpan(start + length, mark.Length).SequenceEqual(mark)
            && !(mark == "*" && start >= 2 && text[start - 2] == '*'))
        {
            return new MarkdownChange(start - mark.Length, length + (mark.Length * 2), selected, start - mark.Length, length);
        }

        var body = length == 0 ? placeholder : selected;

        return new MarkdownChange(start, length, mark + body + mark, start + mark.Length, body.Length);
    }

    private static MarkdownChange Fence(string text, int start, int length)
    {
        // In the text's own line ending; a selection of whole lines keeps its last line ending after the closing fence.
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var whole = text.Substring(start, length);
        var selected = whole.TrimEnd('\n', '\r');
        var after = whole[selected.Length..];
        var before = start > 0 && text[start - 1] != '\n' ? newline : string.Empty;
        var replacement = $"{before}```{newline}{selected}{newline}```{after}";

        return new MarkdownChange(start, length, replacement, start + before.Length + 3 + newline.Length, selected.Length);
    }

    private static MarkdownChange Link(string text, int start, int length)
    {
        const string address = "https://";

        if (length == 0)
        {
            const string label = "link text";

            return new MarkdownChange(start, 0, $"[{label}]({address})", start + 1, label.Length);
        }

        var selected = text.Substring(start, length);

        // A selected address becomes the link's address, with its text to type; any other selection is the text.
        if (selected.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || selected.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            const string label = "link text";

            return new MarkdownChange(start, length, $"[{label}]({selected})", start + 1, label.Length);
        }

        return new MarkdownChange(start, length, $"[{selected}]({address})", start + selected.Length + 3, address.Length);
    }

    // Every line the selection touches gets the mark at its start, after its indent; if every one has it already, it comes off.
    private static MarkdownChange Lines(string text, int start, int length, Func<int, string> mark, Regex existing)
    {
        var lineStart = start == 0 ? 0 : text.LastIndexOf('\n', start - 1) + 1;
        var lastPoint = length > 0 && text[start + length - 1] == '\n' ? start + length - 1 : start + length;
        var lineEnd = text.IndexOf('\n', lastPoint) is var newline and >= 0 ? newline : text.Length;

        // A text box types \r\n: the line ends before its \r, so the caret stays on the line (review of PR 5, finding 2).
        if (lineEnd > lineStart && text[lineEnd - 1] == '\r')
        {
            lineEnd--;
        }
        var block = text[lineStart..lineEnd];
        var lines = block.Split('\n');
        var marked = lines.All(line => line.Trim().Length == 0 || existing.IsMatch(line)) && lines.Any(line => existing.IsMatch(line));
        var number = 0;

        var changed = lines.Select(line =>
        {
            if (marked)
            {
                return existing.Replace(line, "$1", 1);
            }

            if (line.Trim().Length == 0 && lines.Length > 1)
            {
                return line;
            }

            var indent = line.Length - line.TrimStart().Length;
            var bare = AnyMark().Replace(line[indent..], string.Empty, 1);

            return line[..indent] + mark(number++) + bare;
        });
        var replacement = string.Join("\n", changed);

        return length == 0 && lines.Length == 1
            ? new MarkdownChange(lineStart, block.Length, replacement, lineStart + replacement.Length, 0)
            : new MarkdownChange(lineStart, block.Length, replacement, lineStart, replacement.Length);
    }

    [GeneratedRegex(@"^(\s*)#{1,6}\s+")]
    private static partial Regex HeadingMark();

    [GeneratedRegex(@"^(\s*)[-*+]\s+(?!\[[ xX]\]\s)")]
    private static partial Regex BulletMark();

    [GeneratedRegex(@"^(\s*)\d{1,9}[.)]\s+")]
    private static partial Regex NumberMark();

    [GeneratedRegex(@"^(\s*)[-*+]\s+\[[ xX]\]\s+")]
    private static partial Regex CheckMark();

    // A line turned from one kind of list into another loses its old mark.
    [GeneratedRegex(@"^([-*+]\s+\[[ xX]\]\s+|[-*+]\s+|\d{1,9}[.)]\s+|#{1,6}\s+)")]
    private static partial Regex AnyMark();
}
