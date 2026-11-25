using Theseus.Core.Domain;

namespace Theseus.Core.Duties;

/// <summary>What the scheduler needs to know about a card, read from the board at the moment it decides.</summary>
/// <param name="Key">The card's key.</param>
/// <param name="Title">Its title.</param>
/// <param name="Lifecycle">What its session is doing.</param>
/// <param name="IsLive">Whether a session is open.</param>
public sealed record DutyCard(string Key, string Title, SessionLifecycle Lifecycle, bool IsLive)
{
    /// <summary>Whether the owner paused it.</summary>
    public bool PausedByOwner { get; init; }

    /// <summary>The card whose session paused it, or null.</summary>
    public string? PausedByCard { get; init; }

    /// <summary>Whether it is blocked.</summary>
    public bool IsBlocked { get; init; }

    /// <summary>Whether it is archived.</summary>
    public bool IsArchived { get; init; }

    /// <summary>Whether the owner stopped it by hand while its project ran, or its project is stopped.</summary>
    public bool StoppedByOwner { get; init; }

    /// <summary>Whether a question to the owner is open.</summary>
    public bool IsAsking { get; init; }

    /// <summary>Whether a stop request waits for its session.</summary>
    public bool IsStopping { get; init; }

    /// <summary>
    /// Whether an instruction handed to its session waits in the tells folder, not yet taken by its hook: until it is, the
    /// session has not started the turn that carries it, whatever its status says.
    /// </summary>
    public bool TellWaiting { get; init; }

    /// <summary>When its session's status last changed, or null when not known.</summary>
    public DateTimeOffset? StatusSinceUtc { get; init; }

    /// <summary>Whether its session is working or starting.</summary>
    public bool IsBusy => IsLive && Lifecycle is SessionLifecycle.Running or SessionLifecycle.Launching;
}
