namespace Theseus.Core.Projects;

/// <summary>What a slug's <c>work\&lt;slug&gt;\</c> folder holds, as read from the project home.</summary>
/// <param name="Slug">The slug.</param>
/// <param name="Source">Where it was read.</param>
/// <param name="Files">The file names directly in the folder.</param>
public sealed record WorkstreamArtefacts(string Slug, WorkstreamSource Source, IReadOnlySet<string> Files)
{
    /// <summary>fix.md's text, when there is one: its sections say whether a bug's rule and fix are written.</summary>
    public string? FixText { get; init; }

    /// <summary>evidence.md's text, when there is one.</summary>
    public string? EvidenceText { get; init; }

    /// <summary>spec.md's text, when there is one, so View spec shows what Accept spec accepts.</summary>
    public string? SpecText { get; init; }

    /// <summary>The identity of spec.md's content (<see cref="SpecIdentity"/>), when there is one.</summary>
    public string? SpecHash { get; init; }

    /// <summary>The identity of fix.md's Spec section, a bug's rule, when there is one.</summary>
    public string? RuleHash { get; init; }

    /// <summary>The stages its <c>skipped.md</c> says were skipped, with each reason (1.1f E30a).</summary>
    public IReadOnlyDictionary<PipelineStage, string> Skipped { get; init; } = new Dictionary<PipelineStage, string>();

    /// <summary>Whether the project file lists the slug as work from before method-v0.2 (1.1 AC 9).</summary>
    public bool IsBeforeKit { get; init; }

    /// <summary>The project home has no folder for the slug yet.</summary>
    public static WorkstreamArtefacts NotStarted(string slug) =>
        new(slug, WorkstreamSource.None, new HashSet<string>());
}
