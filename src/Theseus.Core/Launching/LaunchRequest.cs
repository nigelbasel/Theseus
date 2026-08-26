namespace Theseus.Core.Launching;

/// <summary>What the launch gate needs to know about a workstream and its state on disk.</summary>
/// <param name="ExistingSessionId">The session the workstream last ran under, if any.</param>
/// <param name="TranscriptExists">
/// Whether a transcript for that session is on disk. Without one there is nothing to resume, however
/// well-formed the identifier looks.
/// </param>
/// <param name="WorkingDirectoryExists">Whether the workstream's working directory is still present.</param>
public sealed record LaunchRequest(
    string? ExistingSessionId,
    bool TranscriptExists,
    bool WorkingDirectoryExists);
