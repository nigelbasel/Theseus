namespace Theseus.Core.Links;

/// <summary>
/// The words a report may end with: Spec Kit's spike verdicts plus the two outcomes a child that is
/// not a spike reports.
/// </summary>
public static class ReportVerdicts
{
    /// <summary>Every verdict a report may carry.</summary>
    public static readonly IReadOnlyList<string> All = ["done", "partial", "go", "needs-clarification", "kill"];

    /// <summary>The verdict as written, if it is one of <see cref="All"/>.</summary>
    public static string? Parse(string? text) =>
        All.FirstOrDefault(verdict => string.Equals(verdict, text?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether a report with this verdict is confirmed in one press; any other opens Mark satisfied.</summary>
    public static bool ConfirmsInOnePress(string? verdict) => verdict is "done" or "go";
}
