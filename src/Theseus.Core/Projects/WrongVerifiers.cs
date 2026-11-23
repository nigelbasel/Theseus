namespace Theseus.Core.Projects;

/// <summary>
/// Which verifier cards should not be kept (git-22, 2026-11-23): one that ran another project's holdout, which happened
/// while one holdout folder covered every project, and every copy of a build's check but the newest correct one, which
/// repeated presses of Start the checks… made. A verifier whose report was confirmed is kept: its verdict is the record.
/// </summary>
public static class WrongVerifiers
{
    /// <summary>The wrong ones among these, oldest first within each build.</summary>
    /// <param name="verifiers">Every verifier card.</param>
    /// <param name="holdoutOf">The holdout folder of the card a verifier checks, or null when it is not known.</param>
    /// <param name="ownerOf">
    /// The project a holdout folder is the own folder of (beside its home repo, or chosen for it alone), or null. A folder
    /// set for every project still belongs to its owner, so another project's verifier that ran there ran the wrong
    /// scenarios (live read, 2026-11-23).
    /// </param>
    public static IReadOnlyList<WrongVerifier> Among(IEnumerable<VerifierRecord> verifiers, Func<string, string?> holdoutOf,
        Func<string, string?>? ownerOf = null)
    {
        ArgumentNullException.ThrowIfNull(verifiers);
        ArgumentNullException.ThrowIfNull(holdoutOf);

        var wrong = new List<WrongVerifier>();

        foreach (var build in verifiers.Where(verifier => !verifier.IsArchived)
                     .GroupBy(verifier => verifier.Subject, StringComparer.OrdinalIgnoreCase))
        {
            var expected = holdoutOf(build.Key);
            var elsewhere = build.Where(verifier => RanElsewhere(verifier, expected) || RanAnotherProjects(verifier, ownerOf)).OrderBy(verifier => verifier.CreatedUtc).ToList();
            var right = build.Except(elsewhere).OrderBy(verifier => verifier.CreatedUtc).ToList();
            var kept = right.LastOrDefault();

            wrong.AddRange(elsewhere.Select(verifier => new WrongVerifier(verifier.Key, verifier.Title, verifier.Subject,
                verifier.IsRunning, $"it ran another project's scenarios, in {VerifierRecord.HoldoutOfReport(verifier.ReportPath)}")));
            wrong.AddRange(right.Where(verifier => verifier != kept && !verifier.ReportConfirmed).Select(verifier => new WrongVerifier(
                verifier.Key, verifier.Title, verifier.Subject, verifier.IsRunning, $"a copy: {kept!.Title} checks the same build")));
        }

        return wrong;
    }

    private static bool RanAnotherProjects(VerifierRecord verifier, Func<string, string?>? ownerOf) =>
        ownerOf is not null && VerifierRecord.HoldoutOfReport(verifier.ReportPath) is { } ran && ownerOf(ran) is { } owner
        && !string.Equals(owner, verifier.SubjectProject, StringComparison.OrdinalIgnoreCase);

    private static bool RanElsewhere(VerifierRecord verifier, string? expected) =>
        expected is not null && VerifierRecord.HoldoutOfReport(verifier.ReportPath) is { } ran
        && !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(ran)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected)), StringComparison.OrdinalIgnoreCase);
}
