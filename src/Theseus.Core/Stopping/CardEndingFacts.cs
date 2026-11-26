using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Core.Stopping;

/// <summary>What <see cref="CardEnding"/> decides from, for one card with a session open.</summary>
/// <param name="Key">The card.</param>
/// <param name="Role">Its role.</param>
/// <param name="Lifecycle">What its session is doing.</param>
/// <param name="IdleSinceUtc">When it last went idle, if known.</param>
/// <param name="SessionStartedUtc">When the session running now started.</param>
/// <param name="Links">Every link on the board.</param>
public sealed record CardEndingFacts(
    string Key,
    WorkstreamRole Role,
    SessionLifecycle Lifecycle,
    DateTimeOffset? IdleSinceUtc,
    DateTimeOffset SessionStartedUtc,
    IReadOnlyList<Link> Links)
{
    /// <summary>When it was last handed work while open, if ever.</summary>
    public DateTimeOffset? LastHandInUtc { get; init; }

    /// <summary>Whether something waits to be told to it with its next turn, or a stop is already asked of it.</summary>
    public bool SomethingWaits { get; init; }

    /// <summary>Whether a question it asked the owner is still unanswered.</summary>
    public bool QuestionOpen { get; init; }

    /// <summary>Whether it is its project's lead, which coordinates and is never ended by itself.</summary>
    public bool IsLead { get; init; }

    /// <summary>For a scenario writer, when a scenario in its folder was last written, if ever.</summary>
    public DateTimeOffset? ScenariosWrittenUtc { get; init; }

    /// <summary>Whether its project is unattended, as the owner set it in Project properties (2.4 W2).</summary>
    public bool Unattended { get; init; }

    /// <summary>When its handoff was last written, if it exists.</summary>
    public DateTimeOffset? HandoffWrittenUtc { get; init; }
}
