namespace Theseus.Core.Links;

/// <summary>
/// Where a link stands. Never stored: it is worked out from the links and the cards on every
/// refresh, as a card's lifecycle is.
/// </summary>
public enum LinkState
{
    /// <summary>A spawned link, which records lineage and holds nothing back.</summary>
    Lineage,

    /// <summary>A feeds link whose input has not been accepted.</summary>
    Waiting,

    /// <summary>A feeds link whose upstream has reported, waiting for Nigel to confirm the report.</summary>
    Reported,

    /// <summary>
    /// A feeds link whose upstream ran after the link was made and stopped without reporting. Neutral:
    /// a clean stop at the end of a day is not an alarm.
    /// </summary>
    NoReportYet,

    /// <summary>
    /// As <see cref="NoReportYet"/>, but the upstream has also been finished with: moved to Done,
    /// archived, or its last run ended unexpectedly. This one needs Nigel.
    /// </summary>
    ReportLost,

    /// <summary>A feeds link whose input Nigel has accepted.</summary>
    Satisfied,

    /// <summary>The card at one end was deleted or its plan file has gone. Shown, never acted on.</summary>
    CardMissing,
}
