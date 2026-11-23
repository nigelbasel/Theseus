namespace Theseus.Core.Projects;

/// <summary>
/// A piece of work: one workstreams row of a project file, worked by one card, shown by its name with its stages (1.1d
/// AC 1). A card linked into the project that no project file lists is a piece of its own, with no objective (AC 13).
/// </summary>
/// <param name="Name">Its name: the row's records folder name, or the linked card's name.</param>
/// <param name="CardKey">The key of the card that works it, as the row's Workstream names it.</param>
/// <param name="Workstream">That card's name on screen.</param>
/// <param name="ObjectiveNumber">Its objective's number, or null when no project file lists it.</param>
/// <param name="ObjectiveTitle">Its objective's title, or null when no project file lists it.</param>
/// <param name="Pipeline">The pipeline it runs, or null when its objective has none chosen.</param>
/// <param name="RecordsFolder">The full path of its <c>work\&lt;piece&gt;\</c> folder, or null when there is none.</param>
/// <param name="IsBeforeKit">Whether the project file lists it as work from before method-v0.2.</param>
/// <param name="Stages">Its stages, in pipeline order.</param>
public sealed record PieceOfWork(
    string Name,
    string CardKey,
    string Workstream,
    string? ObjectiveNumber,
    string? ObjectiveTitle,
    PipelineKind? Pipeline,
    string? RecordsFolder,
    bool IsBeforeKit,
    IReadOnlyList<StageNode> Stages)
{
    /// <summary>Its card on the board, or null when none has its key.</summary>
    public PieceCard? Card { get; init; }

    /// <summary>Whether its card's current piece is this one (its last row), so the card's session and actions apply.</summary>
    public bool IsCurrentPiece { get; init; }

    /// <summary>The owner's latest acceptance of its spec, any version, for Properties (1.1e AC 29).</summary>
    public SpecAcceptance? Acceptance { get; init; }

    /// <summary>What its session reported the owner said in its tab, waiting for him to confirm (1.1e AC 28).</summary>
    public ReportedAcceptance? Reported { get; init; }

    /// <summary>Whether a project file lists it; false for a linked card listed nowhere (AC 13).</summary>
    public bool IsListed { get; init; } = true;

    /// <summary>Its kind, shown by its box's title bar (1.1f A1).</summary>
    public PieceKind Kind { get; init; } = PieceKind.Build;

    /// <summary>What its box is titled: what it achieves (1.1f A4).</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>The round it belongs to: its objective's, else its parent's, else the latest (1.1f C14).</summary>
    public int Round { get; init; } = 1;

    /// <summary>Its row's Branch, or null when no project file lists it.</summary>
    public string? Branch { get; init; }

    /// <summary>Its row's Repos, or null when no project file lists it.</summary>
    public string? Repos { get; init; }

    /// <summary>Whether every criterion of its objective is ticked.</summary>
    public bool IsObjectiveDone { get; init; }

    /// <summary>The title of the piece that handed it work, or null.</summary>
    public string? ParentTitle { get; init; }

    /// <summary>The card of the piece it sits under, by hand-over; null for a root.</summary>
    public string? ParentCardKey { get; init; }

    /// <summary>The titles of the pieces it handed work to, in file order (1.1f E26).</summary>
    public IReadOnlyList<string> HandedOnTo { get; init; } = [];

    /// <summary>Whether its card has reported back on the hand-over that gave it this work.</summary>
    public bool HasReportedBack { get; init; }

    /// <summary>
    /// The verifier cards made to check it that still stand, unstarted or with a session: it gets no second (Nigel,
    /// 2026-11-23). Empty for a piece that is not its card's current one.
    /// </summary>
    public IReadOnlyList<PieceCard> PendingVerifiers { get; init; } = [];

    /// <summary>
    /// The verifier whose report on it waits for the owner to confirm or reject, or null. Once it reports it no longer
    /// stands, and the box read "has not run yet" with Theseus to act (review of P1, blocker 2).
    /// </summary>
    public PieceCard? CheckReported { get; init; }

    /// <summary>Whether that report was confirmed by the owner on the board.</summary>
    public bool IsReportConfirmed { get; init; }

    /// <summary>
    /// A report its session filed after the owner confirmed an earlier one, or null: shown on its box with Open report,
    /// and counted nowhere, since the link is already satisfied (Nigel, 2026-11-23: "Show it on the box, not counted").
    /// </summary>
    public Theseus.Core.Links.LinkReport? LaterReport { get; init; }

    /// <summary>The link the later report came on, by id, so opening it can be remembered; null with none.</summary>
    public string? LaterReportLink { get; init; }

    /// <summary>What its card's session said it is doing, while it is working on this piece; null when it has not said.</summary>
    public SessionStageReport? NowAt { get; init; }

    /// <summary>Whether its spec (a bug's rule) is written and waits for the owner's acceptance.</summary>
    public bool AwaitsSpecAcceptance { get; init; }

    /// <summary>Whether it was reported back and confirmed while its objective's criteria are not all ticked (1.1f E29).</summary>
    public bool IsAnswered => IsReportConfirmed && !IsObjectiveDone && ObjectiveNumber is not null;

    /// <summary>Whether it is freeform: its pipeline is freeform or not chosen yet, so it has the one Work stage.</summary>
    public bool IsFreeform => Pipeline is null or PipelineKind.Freeform;

    /// <summary>"feature", "bug" or "freeform", for its box.</summary>
    public string KindWord => Pipeline switch
    {
        PipelineKind.Feature => "feature",
        PipelineKind.Bug => "bug",
        _ => "freeform",
    };

    /// <summary>"objective 1.1", or "not in project.md" for a linked card listed nowhere.</summary>
    public string ObjectiveLabel => ObjectiveNumber is null ? "not in project.md" : $"objective {ObjectiveNumber}";

    /// <summary>Its status sentence (AC 7).</summary>
    public PieceStatus Status => PieceStatus.For(this);
}
