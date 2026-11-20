namespace Theseus.Core.Projects;

/// <summary>
/// The spec at a workstream's owner gate as it was read: what Open spec shows and Accept spec records (spec AC 7, 15).
/// For a bug it is the rule, fix.md's Spec section.
/// </summary>
/// <param name="Project">The project's name.</param>
/// <param name="Slug">The workstream's slug.</param>
/// <param name="Commit">The last commit that changed it on the branch it was read from.</param>
/// <param name="ContentHash">Its <see cref="SpecIdentity"/>.</param>
/// <param name="Text">Its text.</param>
/// <param name="Source">Where it was read.</param>
public sealed record SpecAtGate(string Project, string Slug, string Commit, string ContentHash, string Text, WorkstreamSource Source)
{
    /// <summary>The commit, short, as the confirmation names it.</summary>
    public string ShortCommit => Commit.Length >= 7 ? Commit[..7] : Commit;

    /// <summary>The spec at the gate of a slug's pipeline, or null when none was read.</summary>
    public static SpecAtGate? For(string project, string slug, PipelineKind? pipeline, WorkstreamArtefacts artefacts)
    {
        ArgumentNullException.ThrowIfNull(artefacts);

        var (text, hash, commit) = pipeline == PipelineKind.Bug
            ? (BugArtefacts.RuleText(artefacts.FixText), artefacts.RuleHash, artefacts.RuleCommit)
            : (artefacts.SpecText, artefacts.SpecHash, artefacts.SpecCommit);

        return text is null || hash is null || commit is null
            ? null
            : new SpecAtGate(project, slug, commit, hash, text, artefacts.Source);
    }

    /// <summary>What Accept spec asks first: the slug and the commit it accepts (AC 15).</summary>
    public string ConfirmationQuestion =>
        $"Accept the spec of {Slug} in {Project}, as committed at {ShortCommit} ({Source.Describe})?"
        + $"{Environment.NewLine}{Environment.NewLine}Its gate then stays passed until the spec changes.";

    /// <summary>The acceptance the owner gives it.</summary>
    public SpecAcceptance Accept(DateTimeOffset now) => new(Project, Slug, Commit, ContentHash, now);
}
