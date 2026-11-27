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

    /// <summary>
    /// The line for sessions started beside an older tab of the same name with nothing running in it, or null for none.
    /// Said, not asked: asking made every Start two presses (Nigel, 2026-11-23).
    /// </summary>
    /// <param name="titles">The titles of the started workstreams that already had such a tab.</param>
    public static string? OldTabLine(IReadOnlyList<string> titles) => titles.Count switch
    {
        0 => null,
        1 => $"{titles[0]} also has an older tab open with nothing running in it; close it when you see it.",
        _ => $"{string.Join(", ", titles)} also have older tabs open with nothing running in them; close them when you see them.",
    };

    /// <summary>
    /// The line for the folders Claude Code will ask the owner to trust in the new tabs, or null when it will ask about
    /// none: said on the board so the question in the tab is expected (project-view-usability item 7, Nigel 2026-11-23).
    /// </summary>
    /// <param name="keys">The trust records the started sessions lack.</param>
    public static string? TrustLine(IReadOnlyList<string> keys) => keys.Count switch
    {
        0 => null,
        1 => $"Claude Code will ask you in the tab to trust {keys[0]}: it is outside the base folders in Options › Folders.",
        _ => $"Claude Code will ask you in the tab to trust {keys.Count} folders ({string.Join(", ", keys)}): they are outside the base folders in Options › Folders.",
    };

    /// <summary>The line for one start the terminal refused.</summary>
    public static string Refusal(string title, string reason) =>
        $"{title} did not start: the terminal would not start ({reason}). The log has the details.";
}
