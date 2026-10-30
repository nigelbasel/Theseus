using Theseus.Core.Links;

namespace Theseus.Core.Domain;

/// <summary>One card as the board last saw it, for readers off the UI thread.</summary>
/// <param name="Workstream">The card's workstream, as loaded.</param>
/// <param name="Handoff">Its handoff, resolved when the snapshot was taken: the stored path, or the one found or proposed.</param>
/// <param name="Lifecycle">What the card showed.</param>
/// <param name="IsLive">Whether a session was open for it.</param>
/// <param name="IsDone">Whether it sat in the board's last stage.</param>
/// <param name="LastRunStartedUtc">When its newest run started, if it has run.</param>
/// <param name="LastRunCrashed">Whether that run ended unexpectedly.</param>
public sealed record BoardSnapshotCard(
    Workstream Workstream,
    string Handoff,
    SessionLifecycle Lifecycle,
    bool IsLive,
    bool IsDone,
    DateTimeOffset? LastRunStartedUtc,
    bool LastRunCrashed)
{
    /// <summary>The card's key.</summary>
    public string Key => Workstream.Key;

    /// <summary>The card's title.</summary>
    public string Title => Workstream.Title;

    /// <summary>What link status needs to know about it.</summary>
    public LinkCard ToLinkCard() =>
        new(Key, Title, Workstream.IsArchived, Workstream.IsBlocked || Workstream.IsPaused, IsLive, LastRunStartedUtc, IsDone, LastRunCrashed);
}
