using Theseus.Core.Domain;
using Theseus.Core.Launching;

namespace Theseus.Core.Running;

/// <summary>A card handed to the runner to run headless, with everything its launch was given.</summary>
/// <param name="Workstream">The card, as it was when started.</param>
/// <param name="Spec">Its launch, built exactly as a tab's is.</param>
/// <param name="Reason">What asked for it.</param>
/// <param name="StopText">What it is told when asked to stop.</param>
public sealed record HeadlessSubmission(Workstream Workstream, LaunchSpec Spec, RunReason Reason, string StopText)
{
    /// <summary>The project's piece of work the card is on, when known.</summary>
    public string? Piece { get; init; }
}
