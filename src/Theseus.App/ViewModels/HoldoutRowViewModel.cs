using Theseus.Core.Links;

namespace Theseus.App.ViewModels;

/// <summary>
/// One project's holdout folder as Options lists it, read-only (options-made-clear item 1): where it is and how it was
/// found. It is changed in the project's Project properties.
/// </summary>
/// <param name="Project">The project's folder name, or "ad-hoc".</param>
/// <param name="Folder">The folder in force; empty when a recorded repository's place is not known.</param>
/// <param name="Source">How it was found.</param>
public sealed record HoldoutRowViewModel(string Project, string Folder, HoldoutSource Source)
{
    /// <summary>Whether a chosen folder or the recorded repository's clone is not there (review, finding 3).</summary>
    public bool IsMissing { get; init; }

    /// <summary>The folder, or why there is none.</summary>
    public string Value => Folder.Length == 0 ? "Not found" : IsMissing ? $"{Folder} (not there)" : Folder;

    /// <summary>How it was found, and where it is changed.</summary>
    public string Explanation => IsAdHoc
        ? "Cards in no project always use Theseus's own ad-hoc folder."
        : $"{Capitalised(HoldoutFolderEntry.HowFoundOf(Source))}{(IsMissing ? ", but it is not there" : string.Empty)}. Change it in the project's Project properties.";

    /// <summary>Whether it is the ad hoc cards' folder, which is always Theseus's own (review of PR 7, finding 3).</summary>
    public bool IsAdHoc => string.Equals(Project, HoldoutLocation.AdHocFolder, StringComparison.OrdinalIgnoreCase);

    private static string Capitalised(string text) => $"{char.ToUpperInvariant(text[0])}{text[1..]}";
}
