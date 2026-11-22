namespace Theseus.Core.Projects;

/// <summary>Reads a slug's <c>work\&lt;slug&gt;\</c> folder in a project home, as plain files (1.1b AC 2 and 4).</summary>
public static class WorkstreamArtefactReader
{
    /// <summary>The folder a project home keeps its workstreams' records in.</summary>
    public const string WorkFolder = "work";

    /// <summary>Reads one slug's artefacts.</summary>
    /// <param name="projectFolder">The project home.</param>
    /// <param name="project">The project, as read from its project.md.</param>
    /// <param name="slug">The slug.</param>
    public static WorkstreamArtefacts Read(string projectFolder, ProjectFile project, string slug)
    {
        ArgumentNullException.ThrowIfNull(projectFolder);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(slug);

        var folder = Path.Combine(projectFolder, WorkFolder, slug);

        if (!Directory.Exists(folder))
        {
            return WorkstreamArtefacts.NotStarted(slug);
        }

        var files = Directory.EnumerateFiles(folder)
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        string? Text(string name) => files.Contains(name) ? File.ReadAllText(Path.Combine(folder, name)) : null;

        var spec = Text("spec.md");
        var fix = Text("fix.md");
        var rule = BugArtefacts.RuleText(fix);

        return new WorkstreamArtefacts(slug, new WorkstreamSource(folder), files)
        {
            FixText = fix,
            SpecText = spec,
            EvidenceText = Text("evidence.md"),
            SpecHash = spec is null ? null : SpecIdentity.Hash(spec),
            RuleHash = rule is null ? null : SpecIdentity.Hash(rule),
            IsBeforeKit = project.BeforeKit.Contains(slug),
            Skipped = SkippedStages.Read(Text(SkippedStages.FileName)),
        };
    }
}
