namespace Theseus.Core.Projects;

/// <summary>
/// Which verifier cards should not be kept (git-22, 2026-11-23): one that ran a holdout another project owns, which
/// happened while one holdout folder covered every project, and every copy of a build's check but the newest correct one,
/// which repeated presses of Start the checks… made. A copy whose report was confirmed is kept: its verdict is the record.
/// One of another project's holdout goes even when confirmed, since it checked the wrong scenarios.
/// </summary>
public static class WrongVerifiers
{
    /// <summary>The wrong ones among these, oldest first within each build.</summary>
    /// <param name="verifiers">Every verifier card.</param>
    /// <param name="ownerOf">
    /// The project a holdout folder is the own folder of (beside its home repo, or chosen for it alone), or null. A folder
    /// set for every project still belongs to its owner. Only ownership counts, not the project's holdout of today: a
    /// project whose holdout moved still ran its own scenarios before (review of item 5, S6).
    /// </param>
    public static IReadOnlyList<WrongVerifier> Among(IEnumerable<VerifierRecord> verifiers, Func<string, string?> ownerOf)
    {
        ArgumentNullException.ThrowIfNull(verifiers);
        ArgumentNullException.ThrowIfNull(ownerOf);

        var wrong = new List<WrongVerifier>();

        foreach (var build in verifiers.Where(verifier => !verifier.IsArchived)
                     .GroupBy(verifier => verifier.Subject, StringComparer.OrdinalIgnoreCase))
        {
            var elsewhere = build.Where(verifier => RanAnotherProjects(verifier, ownerOf)).OrderBy(verifier => verifier.CreatedUtc).ToList();
            // One made before Checks lines is not today's check, even once it has reported, unless he confirmed its report: its
            // report would otherwise stand as the build's check, and no verifier is ever made (git-22, 2026-11-23 20:01). One
            // of today's whose build is only unnamed goes when it has neither reported nor a session, which a rewrite can cause.
            var stranded = build.Except(elsewhere)
                .Where(verifier => !verifier.ReportConfirmed
                                   && (!verifier.BriefNamesChecks || (!verifier.SubjectNamed && !verifier.IsRunning && !verifier.HasReport)))
                .OrderBy(verifier => verifier.CreatedUtc).ToList();
            var right = build.Except(elsewhere).Except(stranded).OrderBy(verifier => verifier.CreatedUtc).ToList();
            var kept = right.LastOrDefault();

            wrong.AddRange(elsewhere.Select(verifier => new WrongVerifier(verifier.Key, verifier.Title, verifier.Subject,
                verifier.IsRunning, $"it ran another project's scenarios, in {VerifierRecord.HoldoutOfReport(verifier.ReportPath)}")));
            wrong.AddRange(stranded.Select(verifier => new WrongVerifier(verifier.Key, verifier.Title, verifier.Subject, verifier.IsRunning,
                !verifier.BriefNamesChecks && verifier.HasReport
                    ? "it ran before scenarios named their work, so its report is not a check under today's rules"
                : !verifier.BriefNamesChecks
                    ? "it was made before scenarios named their work, so it has nothing it may run"
                    : "no scenario names its build, so it has nothing it may run")));
            wrong.AddRange(right.Where(verifier => verifier != kept && !verifier.ReportConfirmed).Select(verifier => new WrongVerifier(
                verifier.Key, verifier.Title, verifier.Subject, verifier.IsRunning, $"a copy: {kept!.Title} checks the same build")));
        }

        return wrong;
    }

    private static bool RanAnotherProjects(VerifierRecord verifier, Func<string, string?> ownerOf) =>
        VerifierRecord.HoldoutOfReport(verifier.ReportPath) is { } ran && ownerOf(ran) is { } owner
        && !string.Equals(owner, verifier.SubjectProject, StringComparison.OrdinalIgnoreCase);
}
