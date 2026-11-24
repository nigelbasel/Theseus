namespace Theseus.App.ViewModels;

/// <summary>What the waits checklist's command bar runs, from the project view that opened it.</summary>
/// <param name="Project">The project's title, for the prompt Ask the lead sends.</param>
/// <param name="EditFile">Writes an edit to the project file, with what the status bar says; null where the view cannot write.</param>
/// <param name="AskText">Asks a line of text: title, label and starting value; null when cancelled. Null where it cannot ask.</param>
/// <param name="LeadName">The project's lead card's title, or null when it has none.</param>
/// <param name="AskLead">Tells the lead's session a prompt and brings its tab forward; null when the project has no lead card.</param>
/// <param name="GoToObjective">Shows an objective in the view, by number.</param>
public sealed record WaitsChecklistActions(
    string Project,
    Action<Func<string, string>, string>? EditFile,
    Func<string, string, string, string?>? AskText,
    string? LeadName,
    Action<string>? AskLead,
    Action<string> GoToObjective);
