using Theseus.Core.Domain;

namespace Theseus.Core.Launching;

/// <summary>Where a session starts, as <see cref="StartFolders.Arrange"/> worked it out.</summary>
/// <param name="Workstream">The workstream to launch: its start folder, added folders and opening as arranged.</param>
/// <param name="MemoryDirectory">
/// Claude Code's auto-memory folder of the card's records folder, when the start folder moved away from it, so the session
/// keeps the memory it had; null when nothing moved.
/// </param>
public sealed record StartArrangement(Workstream Workstream, string? MemoryDirectory);
