using Theseus.Core.Links;

namespace Theseus.Core.Projects;

/// <summary>
/// A report back is a mark on the child's box, not a line (1.1d AC 14; Nigel accepted the mockup, 2026-11-20): it names
/// the parent's piece, and says whether the report is in, still to come, or lost.
/// </summary>
/// <param name="ParentPiece">The piece of the card reported back to.</param>
/// <param name="State">Where the report-back link stands (<see cref="LinkStatus.StateOf"/>).</param>
public sealed record ReportBackMark(string ParentPiece, LinkState State)
{
    /// <summary>Whether the report was lost, shown in the Stuck colour.</summary>
    public bool IsLost => State == LinkState.ReportLost;

    /// <summary>"↩ reported back to …", "↩ reports back to …" or "↩ report to … lost".</summary>
    public string Text => State switch
    {
        LinkState.Reported or LinkState.Satisfied => $"↩ reported back to {ParentPiece}",
        LinkState.ReportLost => $"↩ report to {ParentPiece} lost",
        _ => $"↩ reports back to {ParentPiece}",
    };
}
