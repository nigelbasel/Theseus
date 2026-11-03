namespace Theseus.App.Services;

/// <summary>What the board knows from one session's transcript.</summary>
/// <param name="Measured">What a resume would replay; zero for a running session, which is not measured.</param>
/// <param name="Model">The model's friendly name, or empty.</param>
public readonly record struct TranscriptFact(TranscriptTail.Measurement Measured, string? Model);
