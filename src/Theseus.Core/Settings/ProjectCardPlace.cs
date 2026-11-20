namespace Theseus.Core.Settings;

/// <summary>Where a project's card sits on the board: the column and row the owner dragged it to (project-view AC 12).</summary>
/// <param name="Column">The stage column.</param>
/// <param name="Area">The area row.</param>
public sealed record ProjectCardPlace(string Column, string Area);
