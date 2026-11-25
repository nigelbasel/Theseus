namespace Theseus.Core.Running;

/// <summary>
/// How a card's session runs: in a Windows Terminal tab, or headless, with no tab, in Claude Code's print mode
/// (Nigel, 2026-11-24).
/// </summary>
public enum RunMode
{
    /// <summary>An ordinary tabbed session, for work the owner is watching.</summary>
    Tab,

    /// <summary>Claude Code in print mode with streaming JSON in and out, started and read by Theseus.</summary>
    Headless,
}
