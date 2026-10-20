namespace Theseus.Core.Launching;

/// <summary>
/// What the board says after a press of Start. A terminal that refuses to start must be said out
/// loud: from 2026-10-19 Windows denied every direct start of stable Windows Terminal, and the board
/// only ghosted the card, so for a night Start did nothing with nothing to show why.
/// </summary>
public static class StartNotice
{
    /// <summary>The notice for the workstreams that started and the starts that were refused.</summary>
    /// <param name="started">The titles of the workstreams whose tabs were opened.</param>
    /// <param name="refusals">One line per start the terminal refused, from <see cref="Refusal"/>.</param>
    public static string For(IReadOnlyList<string> started, IReadOnlyList<string> refusals)
    {
        var outcome = started.Count switch
        {
            0 when refusals.Count > 0 => null,
            0 => "Nothing was started.",
            1 => $"Started {started[0]}.",
            _ => $"Started {started.Count} sessions.",
        };

        return string.Join(" ", new[] { outcome }.Concat(refusals).OfType<string>());
    }

    /// <summary>The line for one start the terminal refused.</summary>
    public static string Refusal(string title, string reason) =>
        $"{title} did not start: the terminal would not start ({reason}). The log has the details.";
}
