namespace Theseus.Core.Projects;

/// <summary>What Theseus does itself for a project's builds waiting for their independent check, and what it says meanwhile.</summary>
/// <param name="VerifyKeys">The builds' cards to make and start a verifier for: each named by a scenario, idle, with none standing.</param>
/// <param name="MakeWriter">Whether to make and start the project's scenario writer, for builds no scenario names.</param>
/// <param name="Underway">What is happening, for the Waits line, asking nothing of the owner.</param>
/// <param name="StartWriter">The project's standing scenario writer to start, when it is not running and builds still wait for it; or null.</param>
public sealed record CheckPlan(IReadOnlyList<string> VerifyKeys, bool MakeWriter, IReadOnlyList<string> Underway, string? StartWriter = null);
