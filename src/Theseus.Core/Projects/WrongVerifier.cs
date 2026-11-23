namespace Theseus.Core.Projects;

/// <summary>A verifier card Clear up verifiers… takes away, and why.</summary>
/// <param name="Key">Its card.</param>
/// <param name="Title">Its title.</param>
/// <param name="Subject">The card it checks.</param>
/// <param name="IsRunning">Whether its session is running, so it is stopped before it is archived.</param>
/// <param name="Why">Why it should not be kept.</param>
public sealed record WrongVerifier(string Key, string Title, string Subject, bool IsRunning, string Why);
