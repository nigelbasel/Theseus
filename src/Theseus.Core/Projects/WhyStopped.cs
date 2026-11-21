namespace Theseus.Core.Projects;

/// <summary>
/// Why a card with no session is stopped, when that is more than "never started" or "ended" (1.1e AC 12; Nigel,
/// 2026-11-21: "some sort of indication that a particular card has completed its work"). Shown on its status line and
/// in Properties; a done card is marked, not moved, since stage columns are the owner's to place.
/// </summary>
/// <param name="Kind">Which reason.</param>
/// <param name="Line">The line shown, such as "✓ Done: reported back 21 Nov" or "⏸ Project stopped: resumes with Theseus".</param>
public sealed record WhyStopped(WhyStoppedKind Kind, string Line)
{
    /// <summary>The first reason that applies, in AC 12's order, or null when the card's own line says enough.</summary>
    /// <param name="hasSession">Whether it has a session now: a running card is not stopped.</param>
    /// <param name="isInLastStage">Whether it is in the board's last stage.</param>
    /// <param name="reportConfirmedUtc">When its report back was confirmed on the board, or null.</param>
    /// <param name="stoppedProject">The title of the stopped project whose run state holds it, or null.</param>
    /// <param name="stoppedByOwner">Whether the owner stopped it by hand while its project ran.</param>
    public static WhyStopped? For(bool hasSession, bool isInLastStage, DateTimeOffset? reportConfirmedUtc, string? stoppedProject,
        bool stoppedByOwner)
    {
        if (hasSession)
        {
            return null;
        }

        if (reportConfirmedUtc is { } confirmed)
        {
            return new WhyStopped(WhyStoppedKind.Done,
                $"✓ Done: reported back {confirmed.ToLocalTime().ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture)}");
        }

        if (isInLastStage)
        {
            return new WhyStopped(WhyStoppedKind.Done, "✓ Done");
        }

        if (stoppedProject is not null)
        {
            return new WhyStopped(WhyStoppedKind.ProjectStopped, $"⏸ Project stopped: resumes with {stoppedProject}");
        }

        return stoppedByOwner ? new WhyStopped(WhyStoppedKind.StoppedByYou, "Stopped by you") : null;
    }
}
