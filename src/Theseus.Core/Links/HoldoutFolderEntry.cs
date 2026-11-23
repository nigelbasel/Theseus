namespace Theseus.Core.Links;

/// <summary>One holdout folder in force (1.1e AC 6e), as <c>holdout-folders.json</c> lists it and Options shows it.</summary>
/// <param name="Project">The project's folder name, or "ad-hoc" for cards in no project.</param>
/// <param name="Folder">The folder.</param>
/// <param name="Source">How it was found.</param>
public sealed record HoldoutFolderEntry(string Project, string Folder, HoldoutSource Source)
{
    /// <summary>How it was found, as Options and Project properties say it.</summary>
    public string HowFound => HowFoundOf(Source);

    /// <summary>How a folder found this way is described.</summary>
    public static string HowFoundOf(HoldoutSource source) => source switch
    {
        HoldoutSource.Chosen => "you chose it",
        HoldoutSource.Recorded => "recorded in project.md",
        HoldoutSource.BesideRepo => "beside its repo, by convention",
        _ => "Theseus's own, made on first use",
    };
}
