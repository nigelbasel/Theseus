using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

/// <summary>
/// Why a waiting start request no longer applies (sessions-run-the-project item 15): the owner started or resumed the card
/// himself, it was archived or left the requester's project, or either card is gone.
/// </summary>
public static class StartRequestLapse
{
    /// <summary>Why it lapses, in words for the requester, or null while it still applies.</summary>
    /// <param name="card">The paused card as the board has it, or null when it is gone.</param>
    /// <param name="cardIsLive">Whether the card's session is running.</param>
    /// <param name="requester">The asking session's card, or null when it is gone.</param>
    public static string? Why(Workstream? card, bool cardIsLive, Workstream? requester) =>
        card is null ? "it is no longer on the board"
        : card.IsArchived ? "it was archived"
        : cardIsLive || card is not { IsPaused: true, PausedBy: null } ? "the owner started or resumed it"
        : requester?.Project is not { } project || !string.Equals(project, card.Project, StringComparison.OrdinalIgnoreCase)
            ? "it is not in your project any more"
            : null;
}
