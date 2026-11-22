namespace Theseus.Core.Markdown;

/// <summary>One block of a Markdown text, as the preview draws it.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Text">Its text: inline Markdown for most kinds, the code as typed for a code block.</param>
public sealed record MarkdownBlock(MarkdownBlockKind Kind, string Text)
{
    /// <summary>A heading's level, 1 to 6.</summary>
    public int Level { get; init; }

    /// <summary>A numbered item's number.</summary>
    public int Number { get; init; }

    /// <summary>Whether a check box item is ticked.</summary>
    public bool IsChecked { get; init; }

    /// <summary>How deep a list item is nested, from 0.</summary>
    public int Depth { get; init; }

    /// <summary>A table's cells, row by row, the header first; each cell inline Markdown.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
}
