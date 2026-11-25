namespace Theseus.Core.Projects;

/// <summary>A project file as read: readable with its contents, pinned to an older kit, or unreadable with a reason.</summary>
/// <param name="Name">The project's name: its home folder's name, or the projects folder's when that could not be read.</param>
/// <param name="Status">What became of it.</param>
/// <param name="File">The file's contents, when readable.</param>
/// <param name="Reason">Why it is pinned or unreadable, naming the line where there is one.</param>
public sealed record ProjectFileReading(string Name, ProjectReadingStatus Status, ProjectFile? File, string? Reason)
{
    /// <summary>The project home folder it was read from, when it came from one.</summary>
    public string? Folder { get; init; }

    /// <summary>The project file's text as read, which an owner's edit must still find there (1.1c AC 9).</summary>
    public string? Text { get; init; }

    /// <summary>Each listed slug's artefacts, read from the project home; keyed by slug.</summary>
    public IReadOnlyDictionary<string, WorkstreamArtefacts> Artefacts { get; init; } =
        new Dictionary<string, WorkstreamArtefacts>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether it was read in the current shape.</summary>
    public bool IsReadable => Status == ProjectReadingStatus.Readable && File is not null;

    /// <summary>What the owner is shown, worded as the spec words it (AC 2 and 3).</summary>
    public string Describe => Status switch
    {
        ProjectReadingStatus.Readable => $"{Name}: readable",
        ProjectReadingStatus.Pinned => $"{Name}: pinned to {MethodKits.Pinned}",
        _ => $"{Name}: unreadable: {Reason}",
    };

    /// <summary>A file read in the current shape.</summary>
    public static ProjectFileReading Read(ProjectFile file) => new(file.Name, ProjectReadingStatus.Readable, file, null);

    /// <summary>A file pinned to <see cref="MethodKits.Pinned"/>.</summary>
    public static ProjectFileReading PinnedTo(string name) =>
        new(name, ProjectReadingStatus.Pinned, null, $"its Kit: line names {MethodKits.Pinned}");

    /// <summary>A file, or a folder, that could not be read.</summary>
    public static ProjectFileReading Unreadable(string name, string reason) =>
        new(name, ProjectReadingStatus.Unreadable, null, reason);
}
