using System.Text.Json.Serialization;

namespace Theseus.Core.Stopping;

/// <summary>
/// What the hook reads from its standard input. Claude Code sends a good deal more; only these are
/// taken, so a field added in a later build cannot break the parse.
/// </summary>
/// <param name="SessionId">The session the hook is running inside.</param>
/// <param name="HookEventName">Which event fired, echoed back in the reply.</param>
/// <param name="StopHookActive">
/// Whether this turn was already extended by a stop hook. Delivering again inside a turn the hook
/// itself caused would be a loop.
/// </param>
/// <param name="AgentId">
/// Present when a subagent is running rather than the session itself. A subagent is not the session
/// the user asked to stop, and telling one to write a handoff and exit would end the wrong thing.
/// </param>
public sealed record StopHookInput(
    [property: JsonPropertyName("session_id")] string? SessionId,
    [property: JsonPropertyName("hook_event_name")] string? HookEventName,
    [property: JsonPropertyName("stop_hook_active")] bool StopHookActive = false,
    [property: JsonPropertyName("agent_id")] string? AgentId = null);
