namespace Theseus.Core.Settings;

/// <summary>What the board's rows show (Nigel, 2026-10-30, Option E: View > Rows by).</summary>
public enum BoardRowsBy
{
    /// <summary>The kind of work: the configured areas. Cards can be dragged between them.</summary>
    Area,

    /// <summary>The folder the work is in, from each workstream's working directory.</summary>
    Project,

    /// <summary>The chain of hand-overs each workstream belongs to, named after the card it starts from.</summary>
    Chain,

    /// <summary>One row holding everything.</summary>
    None,
}
