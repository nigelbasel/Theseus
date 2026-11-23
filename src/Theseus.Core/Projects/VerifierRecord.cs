using Theseus.Core.Links;

namespace Theseus.Core.Projects;

/// <summary>A verifier card as Clear up verifiers… judges it.</summary>
/// <param name="Key">Its card.</param>
/// <param name="Title">Its title.</param>
/// <param name="Subject">The card it checks: its report-back link's other end.</param>
/// <param name="SubjectProject">The project of the card it checks, or null for an ad hoc card.</param>
/// <param name="IsRunning">Whether its session is running.</param>
/// <param name="IsArchived">Whether it is archived already.</param>
/// <param name="CreatedUtc">When it was made: its report-back link's creation.</param>
/// <param name="ReportPath">Its report document, in the holdout it ran against.</param>
/// <param name="ReportConfirmed">Whether the owner confirmed its report.</param>
public sealed record VerifierRecord(
    string Key,
    string Title,
    string Subject,
    string? SubjectProject,
    bool IsRunning,
    bool IsArchived,
    DateTimeOffset CreatedUtc,
    string? ReportPath,
    bool ReportConfirmed)
{
    /// <summary>
    /// The holdout a verifier ran against: the folder above the results folder its report is in, as Verify with holdouts…
    /// names it; null for a path of any other shape.
    /// </summary>
    public static string? HoldoutOfReport(string? reportPath)
    {
        if (string.IsNullOrWhiteSpace(reportPath) || Path.GetDirectoryName(reportPath) is not { } results
            || !string.Equals(Path.GetFileName(results), VerifierCard.ResultsFolder, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Path.GetDirectoryName(results);
    }
}
