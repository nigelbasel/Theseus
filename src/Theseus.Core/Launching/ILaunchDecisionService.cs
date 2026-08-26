using Theseus.Core.Sessions;

namespace Theseus.Core.Launching;

/// <summary>
/// Decides whether a workstream may be started, and how. Resuming a session that is still running
/// forks its transcript silently, so a launch requires positive proof that the session is dead
/// rather than an absence of evidence that it is alive.
/// </summary>
public interface ILaunchDecisionService
{
    /// <summary>Decides how to start a workstream, given the live view of Claude Code sessions.</summary>
    LaunchDecision Decide(LaunchRequest request, SessionRegistrySnapshot snapshot);
}
