namespace Theseus.Tests.Windows;

/// <summary>The names of the secondary windows, as test cases.</summary>
public static class SecondaryWindowNames
{
    /// <summary>Every window of <see cref="SecondaryWindows.All"/>.</summary>
    public static IEnumerable<string> Every() =>
    [
        "About", "AddObjective", "AddToProject", "Arrange", "Checklist", "DayReport", "FirstRun", "Instructions", "NewProject", "NewWorkstream",
        "PromptEdit", "Proposal", "Records", "Spec", "Waits",
    ];
}
