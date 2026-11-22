namespace Theseus.Core.Markdown;

/// <summary>
/// What a toolbar button does to the text: replace one range, then select another. The box applies it as a typed
/// replacement, so Undo takes it back.
/// </summary>
/// <param name="Start">Where the replaced range starts.</param>
/// <param name="Length">How long it is.</param>
/// <param name="Replacement">What replaces it.</param>
/// <param name="SelectionStart">Where the selection starts afterwards.</param>
/// <param name="SelectionLength">How long it is afterwards.</param>
public sealed record MarkdownChange(int Start, int Length, string Replacement, int SelectionStart, int SelectionLength)
{
    /// <summary>The text with the change made.</summary>
    public string ApplyTo(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return string.Concat(text.AsSpan(0, Start), Replacement, text.AsSpan(Start + Length));
    }
}
