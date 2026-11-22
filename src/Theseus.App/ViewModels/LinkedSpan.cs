namespace Theseus.App.ViewModels;

/// <summary>A run of a sentence in the project view, and where it goes when it is a link (I42); null for plain text.</summary>
/// <param name="Text">The run's text.</param>
/// <param name="Follow">What following it does, or null.</param>
/// <param name="ToolTip">Where it goes, said in words, or null.</param>
public sealed record LinkedSpan(string Text, Action? Follow, string? ToolTip);
