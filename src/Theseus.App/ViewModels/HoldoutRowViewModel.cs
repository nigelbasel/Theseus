namespace Theseus.App.ViewModels;

/// <summary>One project's holdout folder in Options (1.1e AC 6e): where it is and how it was found, with Change… and Reset.</summary>
/// <param name="Project">The project's folder name, or "ad-hoc".</param>
/// <param name="Folder">The folder in force, or the one chosen here and not saved yet.</param>
/// <param name="HowFound">"chosen by you", "found beside the repo" or "Theseus's own".</param>
/// <param name="IsChosenHere">Whether the owner chose it for this project, so Reset applies.</param>
public sealed record HoldoutRowViewModel(string Project, string Folder, string HowFound, bool IsChosenHere)
{
    /// <summary>The line Options shows under the project's name.</summary>
    public string Line => HowFound.Length == 0 ? Folder : $"{Folder} ({HowFound})";

    /// <summary>Whether it is Theseus's own folder, which can be kept in a private repository.</summary>
    public bool IsTheseusOwn { get; init; }

    /// <summary>Whether it is the ad hoc cards' folder, which is always Theseus's own (review of PR 7, finding 3).</summary>
    public bool IsAdHoc => string.Equals(Project, Theseus.Core.Links.HoldoutLocation.AdHocFolder, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether Change… applies.</summary>
    public bool CanChange => !IsAdHoc;

    /// <summary>Change…'s tooltip: what it does, or why it cannot.</summary>
    public string ChangeToolTip => IsAdHoc
        ? "Cards in no project always use Theseus's own ad-hoc folder"
        : "Choose this project's holdout folder: only if you understand what the guard then protects";

    /// <summary>Reset's tooltip: what it does, or why it cannot.</summary>
    public string ResetToolTip => IsAdHoc
        ? "Cards in no project always use Theseus's own ad-hoc folder"
        : IsChosenHere
            ? "Forget the folder you chose for this project: it is found by convention again, once saved"
            : "Nothing chosen for this project: its folder is found by convention";
}
