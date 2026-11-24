using Theseus.Core.Budgets;

namespace Theseus.Core.Running;

/// <summary>What a headless run is started with.</summary>
/// <param name="RunId">Its identifier.</param>
/// <param name="Workstream">The card's key.</param>
/// <param name="Project">The card's project, or null.</param>
/// <param name="SessionId">The session it runs under.</param>
/// <param name="Reason">What asked for it.</param>
/// <param name="Limits">The limits it works inside.</param>
/// <param name="StopText">What it is told when asked to stop: the board's stop request, with its handoff path.</param>
public sealed record HeadlessRunStart(
    Guid RunId,
    string Workstream,
    string? Project,
    string SessionId,
    RunReason Reason,
    RunLimits Limits,
    string StopText)
{
    /// <summary>The project's piece of work the card is on, when known.</summary>
    public string? Piece { get; init; }

    /// <summary>Whether this is the one resume after a hard cost stop that only writes the handoff.</summary>
    public bool IsHandoffRun { get; init; }

    /// <summary>How long it may take to begin a session before it counts as a failed start.</summary>
    public TimeSpan StartAllowance { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>How long past its time limit it may go before it is interrupted.</summary>
    public TimeSpan OverTimeGrace { get; init; } = TimeSpan.FromMinutes(15);
}
