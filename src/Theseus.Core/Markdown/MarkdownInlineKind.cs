namespace Theseus.Core.Markdown;

/// <summary>The kinds of inline span the preview draws (1.1e AC 23).</summary>
public enum MarkdownInlineKind
{
    /// <summary>Plain text.</summary>
    Text,

    /// <summary>Bold.</summary>
    Strong,

    /// <summary>Italic.</summary>
    Emphasis,

    /// <summary>Inline code.</summary>
    Code,

    /// <summary>A link, with its address.</summary>
    Link,
}
