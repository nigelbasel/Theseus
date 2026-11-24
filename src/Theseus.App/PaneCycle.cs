namespace Theseus.App;

/// <summary>
/// Which pane F6 and Shift+F6 move to (baseline finding B8; checklist G71): the next or previous pane that is shown, in
/// reading order, round the end.
/// </summary>
public static class PaneCycle
{
    /// <summary>The index of the pane to move to, or null when no pane is shown.</summary>
    /// <param name="shown">Whether each pane is shown, in reading order.</param>
    /// <param name="current">The pane that holds the focus now, or null when none does.</param>
    /// <param name="forward">True for F6, false for Shift+F6.</param>
    public static int? Next(IReadOnlyList<bool> shown, int? current, bool forward)
    {
        ArgumentNullException.ThrowIfNull(shown);

        int count = shown.Count;
        int start = current ?? (forward ? -1 : count);

        for (int step = 1; step <= count; step++)
        {
            int index = ((forward ? start + step : start - step) % count + count) % count;

            if (shown[index])
            {
                return index;
            }
        }

        return null;
    }
}
