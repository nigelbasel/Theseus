namespace Theseus.Core.Projects;

/// <summary>
/// What kind of work a piece is, shown by its box's title bar (1.1f A1, A2; D1). Worked out by Theseus from the card's
/// role, the pipeline and the branch, never from the card's name.
/// </summary>
public enum PieceKind
{
    /// <summary>Works out what to build and hands the building on: the feature pipeline with Branch <c>none</c>.</summary>
    Spec,

    /// <summary>Builds a change on its own branch.</summary>
    Build,

    /// <summary>An independent check: a verifier card.</summary>
    Verify,

    /// <summary>Fixes a fault: the bug pipeline.</summary>
    Bug,

    /// <summary>Work with no fixed stages: the freeform pipeline, or none chosen yet.</summary>
    Freeform,
}
