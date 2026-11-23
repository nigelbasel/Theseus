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
    /// <summary>Why it is stopped, such as "Stopped by you", shown in the view's Properties (1.1e AC 12); null when its own line says enough.</summary>
    public string? StopLine { get; init; }

    /// <summary>
    /// Why its last start failed, for a verifier Theseus started itself, or null: the one time a verifier waits for the
    /// owner, with Start again (Nigel, 2026-11-23).
    /// </summary>
    public string? StartFailure { get; init; }

    /// <summary>The question its session has open for the owner (1.6), or null.</summary>
    public Theseus.Core.Questions.OwnerQuestion? Question { get; init; }

    /// <summary>The card whose session paused it, or null (1.1e AC 14): a project's Start skips it while it stands.</summary>
    public string? PausedBy { get; init; }

    /// <summary>When the owner granted its project branch rules, for its mark (1.1e AC 12b); null when not granted.</summary>
    public DateTimeOffset? BranchRulesSince { get; init; }

    /// <summary>Whether it is archived: an archived verifier no longer stands for its build.</summary>
    public bool IsArchived { get; init; }

    /// <summary>
    /// Whether it has ever run: Lifecycle's NotStarted means only "no run today", so a verifier that ran yesterday is
    /// told from one never started by this (review, finding 1).
    /// </summary>
    public bool HasRun { get; init; }

    /// <summary>Whether it has a session open now, so Focus and Stop apply and Start does not.</summary>
    public bool HasSession => Lifecycle is SessionLifecycle.Launching or SessionLifecycle.Running
        or SessionLifecycle.Idle or SessionLifecycle.NeedsAttention;

    /// <summary>Whether its session is working: Busy comes from the session, not the records (code fact 6).</summary>
    public bool IsWorking => Lifecycle is SessionLifecycle.Launching or SessionLifecycle.Running;
}
