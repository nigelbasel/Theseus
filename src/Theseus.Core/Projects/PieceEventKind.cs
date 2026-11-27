namespace Theseus.Core.Projects;

/// <summary>What happened to a piece of work, as <c>piece-events.jsonl</c> records it (7.3).</summary>
public enum PieceEventKind
{
    /// <summary>A session said, with now_at, that its card entered a stage.</summary>
    Stage,

    /// <summary>A session said, with now_at, that a stage was skipped, and why.</summary>
    StageSkipped,

    /// <summary>A criterion was ticked, by a session or by the owner.</summary>
    CriterionTicked,

    /// <summary>A criterion was unticked.</summary>
    CriterionUnticked,

    /// <summary>The owner accepted the piece's spec.</summary>
    SpecAccepted,

    /// <summary>
    /// The owner confirmed a report on the piece that does not finish it: a verdict other than done or go, or a report from
    /// a card that is neither the piece's own nor its verifier.
    /// </summary>
    ReportConfirmed,

    /// <summary>
    /// The piece finished, on an event its implementing session cannot cause: the owner confirmed its verifier's passing
    /// report, or its own report with a verdict of done or go (decision 10, 2026-11-27).
    /// </summary>
    Finished,

    /// <summary>The piece's own card was archived before it finished.</summary>
    Abandoned,
}
