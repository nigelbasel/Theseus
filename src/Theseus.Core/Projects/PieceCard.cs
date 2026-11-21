using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

/// <summary>What the project view needs to know about one board card (1.1d AC 1).</summary>
/// <param name="Key">The card's key, which a project row's Workstream names and links join.</param>
/// <param name="Title">Its name on screen.</param>
/// <param name="Colour">Its colour, as <c>#rrggbb</c>.</param>
/// <param name="Lifecycle">What its session is doing now.</param>
/// <param name="IsBlocked">Whether the owner marked it blocked.</param>
/// <param name="Role">A verifier card is never a piece of work of its own (AC 13).</param>
/// <param name="CanStart">
/// Whether Start would start it, as <c>StartSelection.IsEligible</c> says (code fact 5): not archived, a prompt, and no
/// live session in the registry. The caller fills it from the board, so a ghost or archived card offers no Start.
/// </param>
public sealed record PieceCard(
    string Key,
    string Title,
    string Colour,
    SessionLifecycle Lifecycle,
    bool IsBlocked = false,
    WorkstreamRole Role = WorkstreamRole.None,
    bool CanStart = true)
{
    /// <summary>
    /// Whether the owner marked it paused: a project's Start lists it unticked, since a pause was deliberate (1.1d AC 43,
    /// Nigel's answer to question 4, 2026-11-21).
    /// </summary>
    public bool IsPaused { get; init; }

    /// <summary>Whether it has a session open now, so Focus and Stop apply and Start does not.</summary>
    public bool HasSession => Lifecycle is SessionLifecycle.Launching or SessionLifecycle.Running
        or SessionLifecycle.Idle or SessionLifecycle.NeedsAttention;

    /// <summary>Whether its session is working: Busy comes from the session, not the records (code fact 6).</summary>
    public bool IsWorking => Lifecycle is SessionLifecycle.Launching or SessionLifecycle.Running;
}
