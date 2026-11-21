namespace Theseus.Core.Projects;

/// <summary>
/// One card in a project's Start confirmation (1.1d AC 43; Nigel's answer to question 4, 2026-11-21): ticked when it can
/// start, unticked when paused, greyed with the reason when it cannot start from here.
/// </summary>
/// <param name="Key">The card's key.</param>
/// <param name="Title">Its name on screen.</param>
/// <param name="Reason">Why it is unticked or greyed, as a predicate ("is running", "is paused"), or null.</param>
/// <param name="CanStart">Whether it can be ticked: false for a card running, blocked, done or waiting on the owner.</param>
/// <param name="IsTicked">Whether it starts ticked.</param>
public sealed record ProjectStartEntry(string Key, string Title, string? Reason, bool CanStart, bool IsTicked)
{
    /// <summary>The line the confirmation shows: its name, and its reason when it has one.</summary>
    public string Line => Reason is null ? Title : $"{Title}: {Reason}";
}
