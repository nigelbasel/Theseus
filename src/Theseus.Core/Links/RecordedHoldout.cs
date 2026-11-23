namespace Theseus.Core.Links;

/// <summary>
/// A holdout repository a project's records name (options-made-clear item 4): a row of project.md's Repos table whose role
/// says holdout, or a piece whose Repos cell is "the holdout repository".
/// </summary>
/// <param name="Name">The repository's name when the record gives one, such as <c>Theseus-holdout</c>; null when it is
/// described only ("A private holdout repository"), and the convention's <c>&lt;home repo&gt;-holdout</c> is meant.</param>
public sealed record RecordedHoldout(string? Name);
