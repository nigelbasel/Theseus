namespace Theseus.Core.Projects;

/// <summary>One card a project's Start starts, or skips with the reason (1.1e AC 9).</summary>
/// <param name="Key">The card's key.</param>
/// <param name="Title">Its name on screen, or its key when it is gone.</param>
/// <param name="Reason">Why it is skipped, as a predicate ("is already running"), or null when it starts.</param>
public sealed record ProjectStartEntry(string Key, string Title, string? Reason)
{
    /// <summary>Whether Start starts it.</summary>
    public bool Starts => Reason is null;

    /// <summary>Its name, and its reason when it is skipped.</summary>
    public string Line => Reason is null ? Title : $"{Title} {Reason}";
}
