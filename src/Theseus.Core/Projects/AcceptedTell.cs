namespace Theseus.Core.Projects;

/// <summary>
/// What the card of an accepted spec is told (1.1e AC 27), and how long it waits for a running session's next turn;
/// <see cref="SessionTells"/> queues it, and a stopped card is started with it.
/// </summary>
public static class AcceptedTell
{
    /// <summary>How long a running session's text waits for its next turn.</summary>
    public static readonly TimeSpan Waits = TimeSpan.FromDays(7);

    /// <summary>The text the card is told.</summary>
    public static string Text(SpecAtGate spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        return $"Your {(spec.IsRule ? "rule" : "spec")} (version {spec.ShortVersion}) was accepted: carry on to the plan.";
    }
}
