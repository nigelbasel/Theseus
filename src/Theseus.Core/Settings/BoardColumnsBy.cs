namespace Theseus.Core.Settings;

/// <summary>What the board's columns show (Nigel, 2026-10-30, Option E: View > Columns by).</summary>
public enum BoardColumnsBy
{
    /// <summary>How far along the work is: the configured stages. Cards can be dragged between them.</summary>
    Stage,

    /// <summary>What each session is doing right now: needs you, working, idle, not running, done.</summary>
    SessionState,
}
