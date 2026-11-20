namespace Theseus.Core.Projects;

/// <summary>
/// The project view's columns, in order (spec AC 6). A bug's stages sit under them: reproduce under research, rule under
/// spec, fix under implement; bugs have no plan.
/// </summary>
public enum PipelineStage
{
    /// <summary>research.md; a bug's reproduction.md.</summary>
    Research,

    /// <summary>spec.md; a bug's rule, the Spec section of fix.md.</summary>
    Spec,

    /// <summary>plan.md. Bugs have none.</summary>
    Plan,

    /// <summary>implementation.md; a bug's fix, the Cause or Change section of fix.md.</summary>
    Implement,

    /// <summary>evidence.md.</summary>
    Verify,

    /// <summary>review.md.</summary>
    Review,

    /// <summary>lessons.md.</summary>
    Compound,
}
