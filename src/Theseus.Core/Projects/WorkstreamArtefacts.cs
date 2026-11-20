namespace Theseus.Core.Projects;

/// <summary>What a slug's <c>work/&lt;slug&gt;/</c> folder holds, as read from its source branch.</summary>
/// <param name="Slug">The slug.</param>
/// <param name="Source">Where it was read.</param>
/// <param name="Files">The file names directly in the folder.</param>
public sealed record WorkstreamArtefacts(string Slug, WorkstreamSource Source, IReadOnlySet<string> Files)
{
    /// <summary>fix.md's text, when there is one: its sections say whether a bug's rule and fix are written.</summary>
    public string? FixText { get; init; }

    /// <summary>evidence.md's text, when there is one.</summary>
    public string? EvidenceText { get; init; }

    /// <summary>spec.md's text, when there is one, so Open spec shows what Accept spec accepts.</summary>
    public string? SpecText { get; init; }

    /// <summary>The identity of spec.md's content (<see cref="SpecIdentity"/>), when there is one.</summary>
    public string? SpecHash { get; init; }

    /// <summary>The last commit that changed spec.md.</summary>
    public string? SpecCommit { get; init; }

    /// <summary>The identity of fix.md's Spec section, a bug's rule, when there is one.</summary>
    public string? RuleHash { get; init; }

    /// <summary>The last commit that changed fix.md.</summary>
    public string? RuleCommit { get; init; }

    /// <summary>Whether the work was merged into the primary before the project moved to method-v0.2 (AC 9).</summary>
    public bool IsBeforeKit { get; init; }

    /// <summary>Nothing of the slug is on this machine.</summary>
    public static WorkstreamArtefacts NotOnMachine(string slug) =>
        new(slug, WorkstreamSource.NotOnMachine, new HashSet<string>());
}
