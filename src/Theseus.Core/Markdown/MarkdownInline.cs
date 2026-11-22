namespace Theseus.Core.Markdown;

/// <summary>One inline span of a block's text.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Text">The text shown.</param>
/// <param name="Url">A link's address; null otherwise.</param>
public sealed record MarkdownInline(MarkdownInlineKind Kind, string Text, string? Url = null);
