namespace Theseus.Core.CiStatus;

/// <summary>One line of a card's status drop-down.</summary>
/// <param name="Text">What it says, such as "PR 41 open, ready" or "CI passed 21:43".</param>
/// <param name="Title">The pull request's title, when the line is one.</param>
/// <param name="Url">The page it opens on GitHub, or null when there is none.</param>
/// <param name="Tooltip">What hovering it says.</param>
public sealed record CardStatusRow(string Text, string? Title, string? Url, string Tooltip);
