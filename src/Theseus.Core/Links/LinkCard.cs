namespace Theseus.Core.Links;

/// <summary>What link status needs to know about one card on the board.</summary>
/// <param name="Key">The workstream's key.</param>
/// <param name="Title">Its title, for sentences.</param>
/// <param name="IsArchived">Whether it is retired.</param>
/// <param name="IsBlocked">Whether Nigel marked it blocked.</param>
/// <param name="IsLive">Whether a session is open for it now.</param>
/// <param name="LastRunStartedUtc">When its newest run started, if it has ever run.</param>
public sealed record LinkCard(
    string Key,
    string Title,
    bool IsArchived,
    bool IsBlocked,
    bool IsLive,
    DateTimeOffset? LastRunStartedUtc);
