namespace Theseus.Core.Projects;

/// <summary>
/// The spec at a workstream's owner gate as it was read: what Open spec shows and Accept spec records (spec AC 7, 15).
/// For a bug it is the rule, fix.md's Spec section.
/// </summary>
/// <param name="Project">The project's name.</param>
/// <param name="Slug">The workstream's slug.</param>
/// <param name="ContentHash">Its <see cref="SpecIdentity"/>.</param>
/// <param name="Text">Its text.</param>
/// <param name="Source">Where it was read.</param>
public sealed record SpecAtGate(string Project, string Slug, string ContentHash, string Text, WorkstreamSource Source)
{
    /// <summary>The version the owner is shown: the content hash, short.</summary>
    public string ShortVersion => ContentHash.Length >= 7 ? ContentHash[..7] : ContentHash;

    /// <summary>The spec at the gate of a slug's pipeline, or null when none was read.</summary>
    public static SpecAtGate? For(string project, string slug, PipelineKind? pipeline, WorkstreamArtefacts artefacts)
    {
        ArgumentNullException.ThrowIfNull(artefacts);

        var (text, hash) = pipeline == PipelineKind.Bug
            ? (BugArtefacts.RuleText(artefacts.FixText), artefacts.RuleHash)
            : (artefacts.SpecText, artefacts.SpecHash);

        return text is null || hash is null
            ? null
            : new SpecAtGate(project, slug, hash, text, artefacts.Source);
    }

    /// <summary>What Accept spec asks first: the slug and the version it accepts (AC 15).</summary>
    public string ConfirmationQuestion =>
        $"Accept the spec of {Slug} in {Project}, version {ShortVersion} ({Source.Describe})?"
        + $"{Environment.NewLine}{Environment.NewLine}Its gate then stays passed until the spec changes.";

    /// <summary>The acceptance the owner gives it.</summary>
    public SpecAcceptance Accept(DateTimeOffset now) => new(Project, Slug, ContentHash, now);
}
