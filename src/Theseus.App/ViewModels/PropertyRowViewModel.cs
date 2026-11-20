namespace Theseus.App.ViewModels;

/// <summary>One label and value row of the Properties pane (1.1d AC 34).</summary>
/// <param name="Label">The label, such as "Worker".</param>
/// <param name="Value">Its value, wrapped in the pane.</param>
public sealed record PropertyRowViewModel(string Label, string Value);
