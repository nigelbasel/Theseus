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
    /// <param name="reportPath">The report document.</param>
    /// <param name="recordsFolder">
    /// The checked workstream's records folder in its project home, when it has one; records are kept outside every code
    /// repo (1.1b AC 9).
    /// </param>
    public static string For(string checkedTitle, string reportPath, string? recordsFolder = null) =>
        string.Join(Environment.NewLine,
        [
            $"You are a verifier for {checkedTitle}.",
            string.Empty,
            recordsFolder is null
                ? "Find the checked change's spec in its project home, under work/<slug>/, and its section naming the holdout scenarios that apply."
                : $"Find the checked change's spec in {recordsFolder}, and its section naming the holdout scenarios that apply.",
            "Read the scenarios with the file tools; run builds and tests from the checked card's folder, never from inside the holdout.",
            "Run each named scenario.",
            "Ask the owner in this tab for any step only a person can do, quoting the scenario's step.",
            $"Write the details, steps and observations to {reportPath}.",
            "Report back with a summary that gives only, for each scenario, its name, pass or fail, and one line of reason.",
            "Change nothing in the checked card's repos.",
        ]);
}
