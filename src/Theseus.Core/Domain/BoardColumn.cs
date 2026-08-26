namespace Theseus.Core.Domain;

/// <summary>
/// The board's own columns. These are local status, deliberately not Jira statuses: a workstream
/// routinely names several Jira issues, so "the card's Jira status" would be undefined for half the
/// board, and a board caught disagreeing with Jira is a board that stops being trusted.
/// </summary>
public enum BoardColumn
{
    /// <summary>Queued for today, not yet started.</summary>
    Today,

    /// <summary>Started and being worked.</summary>
    InFlight,

    /// <summary>Set aside, to be picked up another day.</summary>
    Parked,

    /// <summary>Finished.</summary>
    Done,
}
