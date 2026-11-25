using System.Globalization;

namespace Theseus.Core.Projects;

/// <summary>
/// The quiet line a clear-up leaves under the project's heading (Nigel, 2026-11-23): what was done and when Undo ends. It
/// asks nothing of him, so it is not a wait; it lapses after a day, or when the project's next round starts.
/// </summary>
public static class ClearUpNotice
{
    private static readonly string[] Numbers = ["No", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten"];

    /// <summary>How long Undo lasts after the press.</summary>
    public static readonly TimeSpan UndoLasts = TimeSpan.FromHours(24);

    /// <summary>The line, or null when the clear-up has lapsed and can no longer be undone.</summary>
    /// <param name="record">The clear-up kept.</param>
    /// <param name="now">Now, in the owner's own time.</param>
    /// <param name="currentRound">The project's round now.</param>
    public static string? For(VerifierClearUpRecord record, DateTimeOffset now, int currentRound)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (HasLapsed(record, now, currentRound))
        {
            return null;
        }

        var count = record.Cards.Count;
        var ends = (record.AtUtc + UndoLasts).ToOffset(now.Offset);
        var day = ends.Date == now.Date ? "today"
            : ends.Date == now.Date.AddDays(1) ? "tomorrow"
            : ends.ToString("d MMMM", CultureInfo.InvariantCulture);

        return $"{(count < Numbers.Length ? Numbers[count] : count.ToString(CultureInfo.InvariantCulture))} "
               + $"{(count == 1 ? "card was" : "cards were")} cleared up: stopped, archived, {(count == 1 ? "its" : "their")} reports rejected. "
               + $"Undo until {ends.ToString("HH:mm", CultureInfo.InvariantCulture)} {day}.";
    }

    /// <summary>Whether a clear-up can no longer be undone: a day has passed, or the project has moved to a later round.</summary>
    public static bool HasLapsed(VerifierClearUpRecord record, DateTimeOffset now, int currentRound)
    {
        ArgumentNullException.ThrowIfNull(record);

        return now >= record.AtUtc + UndoLasts || currentRound > record.Round;
    }
}
