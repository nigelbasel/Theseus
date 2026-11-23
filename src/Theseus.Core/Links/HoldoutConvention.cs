namespace Theseus.Core.Links;

/// <summary>
/// What a project's holdout folder is found from when the owner chose none (options-made-clear item 4): the primary clone of
/// its home code repo on this machine, and the holdout repository its project.md records. Both come from the project's
/// records and the cards' folders, never from inside a holdout folder.
/// </summary>
/// <param name="HomeClone">The home repo's primary clone, or null when none is found.</param>
/// <param name="Recorded">The holdout repository project.md records, or null when it records none.</param>
public sealed record HoldoutConvention(string? HomeClone, RecordedHoldout? Recorded)
{
    /// <summary>Nothing known: the folder is Theseus's own.</summary>
    public static HoldoutConvention None { get; } = new(null, null);

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
