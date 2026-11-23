namespace Theseus.Core.Links;

/// <summary>
/// A verifier card's fixed brief, in the words of the verifier-card spec (AC 11, approved by Nigel 2026-11-19). Its
/// summary is limited to a verdict per scenario, because the card it checks reads that summary and must not learn the
/// scenarios from it.
/// </summary>
public static class VerifierBrief
{
    /// <summary>The brief for a verifier of the named card, reporting to the given document.</summary>
    /// <param name="checkedTitle">The checked card's title.</param>
    /// <param name="checkedName">The name scenarios check it by in their Checks lines: its slug, or its key.</param>
    /// <param name="reportPath">The report document.</param>
    /// <param name="recordsFolder">
    /// The checked workstream's records folder in its project home, when it has one; records are kept outside every code
    /// repo (1.1b AC 9).
    /// </param>
    public static string For(string checkedTitle, string checkedName, string reportPath, string? recordsFolder = null) =>
        string.Join(Environment.NewLine,
        [
            $"You are a verifier for {checkedTitle}.",
            string.Empty,
            // The scenarios Theseus matched to make this verifier, so the two never disagree (review of the auto-start).
            recordsFolder is null
                ? $"The scenarios to run are those in the holdout whose \"Checks:\" line names {checkedName}; the checked change's spec is in its project home, under work/<slug>/."
                : $"The scenarios to run are those in the holdout whose \"Checks:\" line names {checkedName}; the checked change's spec is in {recordsFolder}.",
            "Read the scenarios with the file tools; run builds and tests from the checked card's folder, never from inside the holdout.",
            "Run each named scenario.",
            "Ask the owner in this tab for any step only a person can do, quoting the scenario's step.",
            $"Write the details, steps and observations to {reportPath}.",
            "Report back with a summary that gives only, for each scenario, its name, pass or fail, and one line of reason.",
            "Change nothing in the checked card's repos.",
        ]);
}
