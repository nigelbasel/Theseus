namespace Theseus.App.ViewModels;

/// <summary>
/// Every line of a project's Waits for you, for the dialog "and n more…" opens (project-view-scale, 2026-11-24): the band
/// shows a few, and lists of items go in dialogs (Nigel's rule). Each line keeps its own button.
/// </summary>
/// <param name="ProjectTitle">The project's title.</param>
/// <param name="Rows">Every line, in the band's order.</param>
/// <param name="ForTheLead">What waits on the project's lead instead, or empty.</param>
public sealed record WaitsListViewModel(string ProjectTitle, IReadOnlyList<WaitItemViewModel> Rows, string ForTheLead)
{
    /// <summary>The dialog's title.</summary>
    public string Title => $"Waits for you: {ProjectTitle}";

    /// <summary>What the dialog is, above the rows.</summary>
    public string Intro =>
        "Everything in this project that waits for you. A line's button does it; a line with none happens outside Theseus.";

    /// <summary>"47 things wait for you.", the count the band's heading and the project's sentence give.</summary>
    public string Foot => Rows.Count == 1 ? "One thing waits for you." : $"{Rows.Count} things wait for you.";

    /// <summary>Whether something waits on the lead, said under the rows.</summary>
    public bool HasForTheLead => ForTheLead.Length > 0;
}
