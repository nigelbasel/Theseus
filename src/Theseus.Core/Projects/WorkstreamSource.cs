namespace Theseus.Core.Projects;

/// <summary>Where a slug's work was read: its <c>work\&lt;slug&gt;\</c> folder in the project home, or none yet.</summary>
/// <param name="Folder">The folder read, or null when the project home has no folder for the slug yet.</param>
public sealed record WorkstreamSource(string? Folder)
{
    /// <summary>The project home has no folder for the slug yet.</summary>
    public static WorkstreamSource None { get; } = new((string?)null);

    /// <summary>Whether its folder exists.</summary>
    public bool Exists => Folder is not null;

    /// <summary>For the card's tooltip.</summary>
    public string Describe => Folder is null ? "no work folder yet" : $"read from {Folder}";
}
