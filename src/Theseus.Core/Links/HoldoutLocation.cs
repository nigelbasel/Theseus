namespace Theseus.Core.Links;

/// <summary>
/// Where a card's holdout scenarios are (1.1e AC 6c; Nigel, 2026-11-21: "why should a user have to set the holdout
/// folder, surely we can use convention ... and allow users to change them if they wish"): the folder the owner chose,
/// kept as it is, or by default <c>%LOCALAPPDATA%\Theseus\holdouts\&lt;project folder name&gt;\</c>, outside every
/// repository and the projects folder, created on first use and covered by the holdout guard as a chosen folder is.
/// </summary>
/// <param name="Folder">The folder.</param>
/// <param name="IsDefault">Whether it is the default, not one the owner chose.</param>
public sealed record HoldoutLocation(string Folder, bool IsDefault)
{
    /// <summary>The default folder of a card in no project.</summary>
    public const string AdHocFolder = "ad-hoc";

    /// <summary>The folder every default holdout folder sits in, which the holdout guard covers whole.</summary>
    public static string DefaultRoot(string? localAppData = null) =>
        Path.Combine(localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Theseus", "holdouts");

    /// <summary>The holdout folder for a project's cards.</summary>
    /// <param name="chosen">The folder set in Options, or empty for the default.</param>
    /// <param name="project">The project's folder name, or null for a card in no project.</param>
    /// <param name="localAppData">Where the default goes, for tests; null for the user's local application data.</param>
    public static HoldoutLocation For(string? chosen, string? project, string? localAppData = null) =>
        string.IsNullOrWhiteSpace(chosen)
            ? new HoldoutLocation(Path.Combine(DefaultRoot(localAppData), string.IsNullOrWhiteSpace(project) ? AdHocFolder : project), true)
            : new HoldoutLocation(chosen.Trim(), false);

    /// <summary>Whether a folder the owner chose is not there: the way on is to set it again.</summary>
    public bool IsMissing => !IsDefault && !Directory.Exists(Folder);

    /// <summary>
    /// Whether it holds any scenario: a file outside the verifiers' results folder and the repository's own folder.
    /// </summary>
    /// <remarks>Asked whenever WPF asks whether Verify can run, so it stops at the first file and never walks .git.</remarks>
    public bool HasScenarios =>
        Directory.Exists(Folder)
        && (Directory.EnumerateFiles(Folder).Any(file => !Path.GetFileName(file).StartsWith(".git", StringComparison.OrdinalIgnoreCase))
            || Directory.EnumerateDirectories(Folder)
                .Where(directory => Path.GetFileName(directory) is var name
                                    && !string.Equals(name, VerifierCard.ResultsFolder, StringComparison.OrdinalIgnoreCase)
                                    && !name.StartsWith(".git", StringComparison.OrdinalIgnoreCase))
                .Any(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any()));

    /// <summary>Creates a default folder on first use; a chosen folder is the owner's and is never created.</summary>
    public void Ensure()
    {
        if (IsDefault)
        {
            Directory.CreateDirectory(Folder);
        }
    }
}
