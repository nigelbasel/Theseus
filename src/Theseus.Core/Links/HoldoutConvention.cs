namespace Theseus.Core.Links;

/// <summary>
/// What a project's holdout folder is found from when the owner chose none (options-made-clear item 4): the primary clone of
/// its home code repo on this machine, and the holdout repository its project.md records. Both come from the project's
/// records and the cards' folders, never from inside a holdout folder.
/// </summary>
/// <param name="HomeClone">The home repo's primary clone, or null when none is found.</param>
/// <param name="Recorded">The holdout repository project.md records, or null when it records none.</param>
public sealed partial record HoldoutConvention(string? HomeClone, RecordedHoldout? Recorded)
{
    /// <summary>Nothing known: the folder is Theseus's own.</summary>
    public static HoldoutConvention None { get; } = new(null, null);

    /// <summary>
    /// A project's convention from its project.md, the folders its building cards work in, and the worktrees folder: the
    /// home repo's clone is looked for among the cards' folders, then among <c>&lt;worktrees&gt;\&lt;feature&gt;\&lt;repo&gt;</c>
    /// worktrees, whose .git file leads to the primary clone. Cards often start in a notes folder that is no clone, so the
    /// worktrees are what finds it then (git-22, 2026-11-23: Theseus's recorded holdout showed no folder).
    /// </summary>
    /// <param name="projectText">The project's project.md, or null.</param>
    /// <param name="cardFolders">The folders the project's building cards work in.</param>
    /// <param name="worktreesFolder">The worktrees folder from Options, or empty.</param>
    /// <param name="recordTexts">
    /// The project's other records, such as its pieces' briefs, read only when nothing else finds the clone: a brief names
    /// the worktree it is built in, so the clone is found even with no worktrees folder set (git-22, 2026-11-23, again).
    /// </param>
    public static HoldoutConvention For(string? projectText, IEnumerable<string> cardFolders, string? worktreesFolder,
        Func<IEnumerable<string>>? recordTexts = null) =>
        For(projectText, cardFolders, worktreesFolder, recordTexts, out _);

    /// <inheritdoc cref="For(string?, IEnumerable{string}, string?, Func{IEnumerable{string}}?)" />
    /// <param name="foundBy">Where the clone was found, for the log: "cards", "worktrees folder", "records" or "nowhere".</param>
    public static HoldoutConvention For(string? projectText, IEnumerable<string> cardFolders, string? worktreesFolder,
        Func<IEnumerable<string>>? recordTexts, out string foundBy)
    {
        var name = HomeRepo.NameIn(projectText);
        var recorded = HomeRepo.RecordedHoldoutIn(projectText);
        var folders = cardFolders.Where(folder => !folder.Contains(HomeRepo.HoldoutSuffix, StringComparison.OrdinalIgnoreCase)).ToList();

        if (HomeRepo.Clone(name, folders) is { } fromCards)
        {
            foundBy = "cards";

            return new HoldoutConvention(fromCards, recorded);
        }

        if (name is null)
        {
            foundBy = "nowhere";

            return new HoldoutConvention(null, recorded);
        }

        IEnumerable<string> worktrees = !string.IsNullOrWhiteSpace(worktreesFolder) && Directory.Exists(worktreesFolder)
            ? Directory.EnumerateDirectories(worktreesFolder).Select(feature => Path.Combine(feature, name)).Where(Directory.Exists)
            : [];

        if (HomeRepo.Clone(name, worktrees) is { } fromWorktrees)
        {
            foundBy = "worktrees folder";

            return new HoldoutConvention(fromWorktrees, recorded);
        }

        var named = (recordTexts?.Invoke() ?? []).Prepend(projectText ?? string.Empty).SelectMany(text => FoldersNamed(text, name));

        if (HomeRepo.Clone(name, named) is { } fromRecords)
        {
            foundBy = "records";

            return new HoldoutConvention(fromRecords, recorded);
        }

        foundBy = "nowhere";

        return new HoldoutConvention(null, recorded);
    }

    // Full paths written in a record, cut back to the folder named after the repo, that are there: E:\work\x\Repo\src
    // gives E:\work\x\Repo. A holdout folder is never a candidate.
    private static IEnumerable<string> FoldersNamed(string text, string repo) =>
        WrittenPath().Matches(text)
            .Select(match => match.Value.TrimEnd('.', ',', ';', ':', '\\'))
            .Select(path => path.Split('\\'))
            .Select(segments => Array.FindIndex(segments, segment => string.Equals(segment, repo, StringComparison.OrdinalIgnoreCase)) is var at and > 0
                ? string.Join('\\', segments.Take(at + 1))
                : null)
            .OfType<string>()
            .Where(folder => !Path.GetFileName(folder).EndsWith(HomeRepo.HoldoutSuffix, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(Directory.Exists);

    [System.Text.RegularExpressions.GeneratedRegex(@"(?<![\w\\])[A-Za-z]:\\[^\s`'""|<>*?()\[\]]+")]
    private static partial System.Text.RegularExpressions.Regex WrittenPath();

    /// <summary>
    /// Where the recorded repository's clone is expected: beside the home clone, by the name recorded, else
    /// <c>&lt;home repo&gt;-holdout</c>. Null when nothing is recorded or the home clone is not known.
    /// </summary>
    public string? RecordedFolder => Recorded is null ? null : BesideHome(Recorded.Name);

    /// <summary>The <c>&lt;home repo&gt;-holdout</c> folder beside the home clone, when it is there.</summary>
    public string? BesideRepo => HomeRepo.HoldoutBeside(HomeClone);

    private string? BesideHome(string? name)
    {
        if (HomeClone is null || Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(HomeClone)) is not { } parent)
        {
            return null;
        }

        return Path.Combine(parent, name ?? $"{Path.GetFileName(Path.TrimEndingDirectorySeparator(HomeClone))}{HomeRepo.HoldoutSuffix}");
    }
}
