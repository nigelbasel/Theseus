namespace Theseus.Core.Links;

/// <summary>
/// A verifier card's fixed brief, in the words of the verifier-card spec (AC 11, approved by Nigel 2026-11-19). Its
/// summary is limited to a verdict per scenario, because the card it checks reads that summary and must not learn the
/// scenarios from it.
/// </summary>
public static class VerifierBrief
{
    /// <summary>The brief for a verifier of the named card, reporting to the given document.</summary>
    public static string For(string checkedTitle, string reportPath) =>
        string.Join(Environment.NewLine,
        [
            $"You are a verifier for {checkedTitle}.",
            string.Empty,
            "Find the checked change's spec under work/ in its repos, and its section naming the holdout scenarios that apply.",
            "Run each named scenario.",
            "Ask the owner in this tab for any step only a person can do, quoting the scenario's step.",
            $"Write the details, steps and observations to {reportPath}.",
            "Report back with a summary that gives only, for each scenario, its name, pass or fail, and one line of reason.",
            "Change nothing in the checked card's repos.",
        ]);
}
