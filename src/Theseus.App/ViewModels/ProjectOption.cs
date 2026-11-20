namespace Theseus.App.ViewModels;

/// <summary>A readable project offered by Add to project…, with its objectives as "1.1 Title".</summary>
/// <param name="Name">The project's name.</param>
/// <param name="Title">Its title.</param>
/// <param name="Objectives">Its objectives: number and title.</param>
public sealed record ProjectOption(string Name, string Title, IReadOnlyList<(string Number, string Title)> Objectives);
