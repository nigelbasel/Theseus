namespace Theseus.Core.Markdown;

/// <summary>The kinds of block the native preview draws (1.1e AC 23).</summary>
public enum MarkdownBlockKind
{
    /// <summary>A heading, with its level.</summary>
    Heading,

    /// <summary>Running text.</summary>
    Paragraph,

    /// <summary>A bulleted list item.</summary>
    Bullet,

    /// <summary>A numbered list item, with its number.</summary>
    Numbered,

    /// <summary>A check box list item, ticked or not.</summary>
    Task,

    /// <summary>A fenced code block, drawn as typed.</summary>
    Code,

    /// <summary>A quote.</summary>
    Quote,

    /// <summary>A table, with its rows; the first is the header.</summary>
    Table,

    /// <summary>A horizontal rule.</summary>
    Rule,
}
