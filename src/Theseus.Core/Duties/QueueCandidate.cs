using Theseus.Core.Domain;

namespace Theseus.Core.Duties;

/// <summary>What the overnight queue needs to know about one card of a project, read from the board.</summary>
/// <param name="Card">What duties need to know about it too.</param>
/// <param name="Project">Its project, or null for ad hoc work.</param>
public sealed record QueueCandidate(DutyCard Card, string? Project)
{
    /// <summary>Whether its project is running, not stopped.</summary>
    public bool ProjectRunning { get; init; }

    /// <summary>Whether it is the current card of a piece of work that is not done.</summary>
    public bool HasUnfinishedPiece { get; init; }

    /// <summary>Whether its piece waits for the owner or is held at a gate.</summary>
    public bool WaitsForOwner { get; init; }

    /// <summary>Whether it is a verifier card, which has its own start.</summary>
    public bool IsVerifier { get; init; }

    /// <summary>When its session's status last changed, which for an idle session is when it went idle.</summary>
    public DateTimeOffset? StatusSinceUtc { get; init; }

    /// <summary>Its piece's title.</summary>
    public string PieceTitle { get; init; } = string.Empty;

    /// <summary>Its piece's status line, such as "Build: merging the duties list".</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Whether its session is open and idle, with no question open.</summary>
    public bool IsQuietlyIdle => Card.IsLive && Card.Lifecycle == SessionLifecycle.Idle && !Card.IsAsking;
}
