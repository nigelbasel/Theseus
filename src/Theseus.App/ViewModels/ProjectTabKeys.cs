namespace Theseus.App.ViewModels;

/// <summary>
/// Which project tab a tab key goes to (baseline W27, finding B7; checklist G75, G76): Ctrl+1 to Ctrl+8 the tab of
/// that number, Ctrl+9 the last tab, as in a browser; Ctrl+Tab and Ctrl+Shift+Tab the next and previous, round the end.
/// </summary>
public static class ProjectTabKeys
{
    /// <summary>The number Ctrl+9 sends, which goes to the last tab however many there are.</summary>
    public const int Last = 9;

    /// <summary>The tab a numbered key goes to, or null when there is no such tab.</summary>
    /// <param name="number">The key's number, 1 to 9, as an int or a string.</param>
    /// <param name="count">How many tabs there are.</param>
    public static int? Numbered(object? number, int count)
    {
        int? key = number switch
        {
            int value => value,
            string text when int.TryParse(text, out int parsed) => parsed,
            _ => null,
        };

        if (key is not { } n || n < 1 || n > Last || count == 0)
        {
            return null;
        }

        if (n == Last)
        {
            return count - 1;
        }

        return n <= count ? n - 1 : null;
    }

    /// <summary>The key that opens the tab at <paramref name="index"/>, for its tooltip, or null when no number key reaches it.</summary>
    /// <param name="index">The tab's index.</param>
    /// <param name="count">How many tabs there are.</param>
    public static string? KeyFor(int index, int count) =>
        index < Last - 1 ? $"Ctrl+{index + 1}" : index == count - 1 ? $"Ctrl+{Last}" : null;

    /// <summary>The tab one step on from the open one, or null when there is nowhere to step to.</summary>
    /// <param name="open">The open tab's index, or null when no project is open.</param>
    /// <param name="count">How many tabs there are.</param>
    /// <param name="forward">True for the next tab, false for the previous.</param>
    public static int? Step(int? open, int count, bool forward)
    {
        if (open is not { } current || count < 2)
        {
            return null;
        }

        return forward ? (current + 1) % count : (current - 1 + count) % count;
    }
}
