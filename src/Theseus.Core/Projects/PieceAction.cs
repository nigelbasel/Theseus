namespace Theseus.Core.Projects;

/// <summary>A piece's single primary action, the button its folded box shows (1.1f F31).</summary>
public enum PieceAction
{
    /// <summary>Nothing for the owner to do from this box.</summary>
    None,

    /// <summary>Open its records folder.</summary>
    OpenRecords,

    /// <summary>Bring its card's terminal forward.</summary>
    Focus,

    /// <summary>Accept its spec (a bug's rule).</summary>
    Accept,

    /// <summary>Answer the question its session asked, in its terminal.</summary>
    Answer,


    /// <summary>Open the evidence that failed.</summary>
    OpenEvidence,

    /// <summary>Start its card.</summary>
    Start,

    /// <summary>Add it to an objective in the project file.</summary>
    AddToObjective,
}
