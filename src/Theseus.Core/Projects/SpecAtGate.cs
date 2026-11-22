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
    /// <summary>Whether this is a bug's rule, from its fix.md, rather than a spec.md.</summary>
    public bool IsRule { get; init; }

    /// <summary>The file it is read from: its spec.md, or the fix.md a bug's rule is in; null with no work folder.</summary>
    public string? FilePath => Source.Folder is { } folder ? Path.Combine(folder, IsRule ? "fix.md" : "spec.md") : null;

    /// <summary>
    /// It as it is on disk now, for the spec window (1.1e AC 24a): null when the file has gone or holds no spec. A
    /// different <see cref="ContentHash"/> means it changed since this was drawn.
    /// </summary>
    public SpecAtGate? ReadAgain()
    {
        if (FilePath is not { } path || !File.Exists(path))
        {
            return null;
        }

        string file;

        try
        {
            file = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return this;
        }

        var text = IsRule ? BugArtefacts.RuleText(file) : file;

        return text is null ? null : this with { Text = text, ContentHash = SpecIdentity.Hash(text) };
    }

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
            : new SpecAtGate(project, slug, hash, text, artefacts.Source) { IsRule = pipeline == PipelineKind.Bug };
    }

    /// <summary>What Accept spec asks first: the slug and the version it accepts (AC 15).</summary>
    public string ConfirmationQuestion =>
        $"Accept the {(IsRule ? "rule" : "spec")} of {Slug} in {Project}, version {ShortVersion} ({Source.Describe})?"
        + $"{Environment.NewLine}{Environment.NewLine}Its gate then stays passed until the {(IsRule ? "rule" : "spec")} changes.";

    /// <summary>
    /// The question before accepting (1.1e AC 27): it names the card Theseus then tells, and, for a report from a session,
    /// the owner's words it quoted.
    /// </summary>
    public string QuestionFor(string? cardTitle, ReportedAcceptance? report = null, bool cardIsRunning = true)
    {
        var noun = IsRule ? "rule" : "spec";
        var said = report is null
            ? string.Empty
            : $"{Environment.NewLine}{Environment.NewLine}{report.CardTitle}'s session reports you said: \"{report.OwnerWords}\"";
        var tells = cardTitle is null
            ? $"{Environment.NewLine}{Environment.NewLine}No card works it, so no session will be told."
            : cardIsRunning
                ? $"{Environment.NewLine}{Environment.NewLine}Theseus then tells {cardTitle} to carry on to the plan."
                : $"{Environment.NewLine}{Environment.NewLine}{cardTitle} is stopped: Theseus then starts it, told to carry on to the plan.";

        return $"Accept the {noun} of {Slug} in {Project}, version {ShortVersion} ({Source.Describe})?{said}"
               + $"{Environment.NewLine}{Environment.NewLine}Its gate then stays passed until the {noun} changes.{tells}";
    }

    /// <summary>The acceptance the owner gives it.</summary>
    public SpecAcceptance Accept(DateTimeOffset now) => new(Project, Slug, ContentHash, now);
}
