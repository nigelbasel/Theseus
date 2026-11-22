namespace Theseus.Core.Links;

/// <summary>One holdout folder in force (1.1e AC 6e), as <c>holdout-folders.json</c> lists it and Options shows it.</summary>
/// <param name="Project">The project's folder name, or "ad-hoc" for cards in no project.</param>
/// <param name="Folder">The folder.</param>
/// <param name="Source">How it was found.</param>
public sealed record HoldoutFolderEntry(string Project, string Folder, HoldoutSource Source)
{
    /// <summary>How it was found, as Options says it.</summary>
    public string HowFound => Source switch
    {
        HoldoutSource.Chosen => "chosen by you",
        HoldoutSource.BesideRepo => "found beside the repo",
        _ => "Theseus's own",
    };
}
