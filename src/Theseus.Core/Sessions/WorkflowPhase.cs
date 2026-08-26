namespace Theseus.Core.Sessions;

/// <summary>One phase a workflow declared for itself.</summary>
/// <param name="Title">The phase's name, as shown in the progress display.</param>
/// <param name="Detail">What the phase does, where the workflow said.</param>
public sealed record WorkflowPhase(string? Title, string? Detail);
