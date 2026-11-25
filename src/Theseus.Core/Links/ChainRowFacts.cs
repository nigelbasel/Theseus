using Theseus.Core.Domain;

namespace Theseus.Core.Links;

/// <summary>What the board knows about one card that decides its Chains row's state and the panel's filters.</summary>
/// <param name="Lifecycle">Its session's lifecycle.</param>
public sealed record ChainRowFacts(SessionLifecycle Lifecycle)
{
    /// <summary>Whether it is paused.</summary>
    public bool IsPaused { get; init; }

    /// <summary>Whether it is blocked.</summary>
    public bool IsBlocked { get; init; }

    /// <summary>Whether it is archived.</summary>
    public bool IsArchived { get; init; }

    /// <summary>Whether it is done (<c>WhyStoppedKind.Done</c>).</summary>
    public bool IsDone { get; init; }

    /// <summary>Whether its inputs are all accepted, or one waits.</summary>
    public CardReadiness Readiness { get; init; }

    /// <summary>Whether a report from it waits for the owner's Confirm.</summary>
    public bool ReportWaiting { get; init; }

    /// <summary>Whether a report from it is lost.</summary>
    public bool ReportLost { get; init; }

    /// <summary>Whether a start of it waits for the owner's Allow.</summary>
    public bool StartAsked { get; init; }
}
