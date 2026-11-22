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
