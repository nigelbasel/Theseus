namespace Theseus.App.ViewModels;

/// <summary>
/// A card of a running project, whose project decides what runs: its Start is the owner's override, Start anyway…, and
/// <paramref name="Line"/> says who starts it and when, or is null where the box keeps its own answer (item 5).
/// </summary>
/// <param name="Line">Who starts it and when, or null.</param>
public sealed record ProjectRunStart(string? Line);
